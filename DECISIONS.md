# Design Decision Log

This document explains the seven key design decisions for the OTP delivery service. Each section follows the same structure: the choice I made, the alternatives I considered, and the tradeoff I accepted.

Written in my own words from notes taken over the 7 days of work.

---

## Decision 1 — Two brokers: Kafka for ingestion, RabbitMQ for delivery

The Task asked for both, but the more important question for me was *why* both, because I had to explain it to myself before I could explain it to anyone else.

Kafka and RabbitMQ are good at different things. Kafka is built for high-volume ingestion — it's an append-only log, and it handles bursts without breaking a sweat. That's exactly what I need at the front door, where producers might dump 20,000 OTP requests on me in one second. Kafka treats that as a normal day.

But once the message is in the system, the job changes. Now I need things Kafka doesn't natively do well: per-message priority (so txn_signing jumps the queue), per-message TTL retries (so I can wait 5s then 30s then 2min between retries without writing my own scheduler), and a dead-letter exchange for messages that exhausted retries. RabbitMQ has all of those out of the box.

## Decision 2 — Kafka topic: 12 partitions, key = msisdn

I picked 12 partitions and used the phone number (msisdn) as the partition key. Two separate decisions,let me explain both:

For the partition count, the math is: each Ingestion consumer does a Redis SETNX, a Postgres INSERT with a publisher-confirmed RabbitMQ publish — realistically ~6-10ms per message, which gives roughly 800-1000 messages/sec per consumer. To hit the 20,000 msg/sec target, I'd need around 20-25 consumers, which means 20-25 partitions.

I chose 12 partitions for this submission knowing it's under-provisioned for the full 20k target — at ~800/sec per consumer that's about 9,600/sec sustained, roughly half the brief's target. The reason: 12 is enough to demonstrate parallelism, prove the partition key works, and run a meaningful load test on a single laptop. Re-partitioning to 24 or 36 is a Kafka admin operation that's straightforward to do once production traffic patterns are clear. Over-partitioning has its own costs (more broker file handles, more rebalance overhead, more consumer coordination), and I'd rather start tight and grow than start wide.

For the key, msisdn matters because it preserves **ordering per phone number**. If a user requests an OTP, then immediately requests another (they didn't get the first one, hit resend), both messages land on the same Kafka partition because they have the same key. Same partition = same consumer = processed in order. That avoids weird races where the second request finishes before the first.

The downside is hot partitions. If one phone number suddenly gets 1,000 OTP requests (probably an attack), all of those land on one partition, and one consumer has to chew through them while the other 11 sit idle. I mitigate this with the cooldown — most of those 1,000 get rejected at Ingestion before they do real work. But the hot-partition risk is real and I'm explicitly accepting it.

## Decision 3 — Idempotency: Redis SETNX before any work, Kafka offset committed after RabbitMQ confirms

At-least-once delivery means: if my consumer crashes after processing a message but before committing the offset, Kafka redelivers that message. So duplicates are not a bug — they're expected. The system has to handle them gracefully.

I handle it in two places. First, before any real work happens, the Ingestion consumer does a Redis `SETNX dedupe:{requestId}` with a TTL. SETNX is atomic — if the key already exists, the SET fails and I know I've seen this request before. I skip the work, commit the Kafka offset, and move on. The TTL is long enough (24 hours) that even slow Kafka re-deliveries get caught.

Second, the order of operations matters. I commit the Kafka offset **only after** RabbitMQ confirms the message was durably accepted. If RabbitMQ fails or my consumer crashes mid-flight, the Kafka offset stays uncommitted, so the next consumer starts re-processes that message. Combined with Redis dedupe, this gives me effectively exactly-once semantics from the producer's perspective.

The Postgres `request_id` unique constraint is still there as a backstop. If something somehow gets past Redis (rare — would mean Redis lost the key), the database refuses the duplicate INSERT. Two layers: Redis is the fast first defense, Postgres is the safety net.

What I gave up: at-most-once delivery would be simpler — just process once and accept that crashes lose messages. For OTPs, that's not acceptable. Users need their codes. I'd rather have occasional duplicate work than dropped OTPs.

## Decision 4 — Three-band priority derived from purpose, not producer-supplied

I have three priority bands — High (10), Normal (5), Low (1) — mapped to a RabbitMQ priority queue with `x-max-priority=10`. The mapping is based on the `purpose` field of the request, not on a priority number the producer sends.

This matters. If I let producers send their own priority, within a month every producer would mark everything as P0 ("our service is the most important one"). Priority becomes meaningless. By mapping `txn_signing` → High, `login` → Normal, `bulk_notify` → Low on my side, the priority reflects what the OTP is *for*, not who's sending it.

Producers can still influence priority, but only downward. If they send `Priority: 1` on a login OTP, I'll honor it — they're voluntarily saying "this isn't urgent, send it whenever." But if they send `Priority: 10` on a `bulk_notify`, I ignore it. Downgrades yes, upgrades no.

RabbitMQ enforces the priority at the queue level. Messages with priority 10 are dequeued before messages with priority 5, which are dequeued before priority 1. So during a burst, a txn_signing OTP that arrives last can still be delivered first.

The tradeoff is starvation under sustained overload. If the queue is constantly filling with High-priority messages faster than they can drain, Low-priority will be delayed indefinitely. I accept this — `bulk_notify` is promotional, latency-tolerant by nature. Decision 4 protects security-critical OTPs at the cost of bulk OTP latency.

## Decision 5 — Retry strategy

I built 5 retry tiers using RabbitMQ's TTL + DLX mechanism. When delivery fails transiently, I republish the message to a retry queue with a TTL (5s, 30s, 2min, 10min, 30min — exponential). When the TTL expires, RabbitMQ automatically dead-letters the message back to the main delivery queue, where the consumer picks it up again. So the retry "wait" doesn't block any worker — it lives entirely in the broker.

The classification of failures matters. A 4xx from Meta (permanent — invalid phone number, malformed template) goes straight to the dead-letter queue, no retries. A 5xx or 429 (transient) goes to the next retry tier. Network exceptions (timeout, connection refused) count as transient. After all 5 tiers are exhausted, the message dead-letters with reason "Retries exhausted".

The attempt counter lives in the message header (`x-attempt`), not the body. Two reasons: I don't want to re-serialize the message JSON on every retry, and headers are easier to inspect in the RabbitMQ management UI when debugging.

The `OtpRecord.Status` only changes at terminal states — `Sent` on success, `DeadLettered` on permanent failure or exhausted retries. While retries are in flight, the record stays `Queued`. This way the status endpoint correctly reports "still trying" instead of bouncing between states.

What I gave up: I'm not using `Thread.Sleep` for retry backoff, which would be simpler but would block the consumer thread for 30 minutes on the last tier. The TTL+DLX approach is more complex but keeps consumers free to process other messages.

## Decision 6 — OTP storage: SHA-256 + per-OTP salt + global pepper, hash only

The plaintext OTP exists in exactly two places in my system: in memory while the worker is processing it, and in the outbound HTTP payload to Meta. After that, it's gone. I never store it.

The reason for both salt and pepper: if an attacker dumps the database, they have the hashes and salts but not the pepper (it's in environment config, not DB). They can't brute-force the hashes without it. If they get the pepper but not the database, they have nothing to attack. Both have to be compromised together to be useful — that's defense in depth.

The constant-time compare matters because string equality (`==`) returns as soon as the first byte differs. An attacker measuring response times can learn the hash byte-by-byte. `FixedTimeEquals` always compares all bytes regardless of where the mismatch is, so timing is uniform.

OTPs are not encrypted, just hashed. Encryption would mean I could decrypt and read the original code on the server, which means anyone with admin access could read users' OTPs. Hashing makes that impossible by design — even I can't recover the original.

What I gave up: the verify endpoint can't tell the user "your code was off by one digit" because it doesn't know the original. It can only say valid or invalid. That's the right tradeoff — better UX is not worth giving the server access to plaintext OTPs.

## Decision 7 — Webhook ordering: trust Meta wall-clock timestamp, not arrival order

Meta sends a webhook every time a message changes status: sent, delivered, read, failed. These webhooks are HTTP POSTs, and HTTP doesn't guarantee ordering. So I can receive a `failed` webhook at 10:00:05 and then a `sent` webhook at 10:00:08 — even though Meta's timestamps inside the payloads are `10:00:03` for sent and `10:00:05` for failed.

The wrong answer is "last arrival wins." That would say final state is `sent` (the one that arrived last), but Meta actually told me the message failed. Trusting arrival order would silently lie to my users.

The right answer is to trust Meta's timestamp. Each `OtpRecord` has a `last_applied_timestamp` column. Before applying any webhook update, I compare `incoming_timestamp` against `last_applied_timestamp`. If the incoming one is older, I skip — I've already applied a newer update. If newer, I apply and update the column.

This makes webhook handling idempotent and order-independent. The same webhook can arrive twice and only have effect once. Webhooks can arrive in any order and the final state matches what Meta actually said happened.

There's a secondary guard for terminal states that don't come from Meta (Verified, Locked, Expired — those come from the verify endpoint). These have no Meta timestamp, so they're protected by a simple status check: don't overwrite Verified/Locked/Expired regardless of what the webhook says.

What I gave up: this assumes Meta's clocks are reasonably accurate. If Meta's servers ever drift backwards in time, I could skip legitimate updates. I judged this acceptable — Meta is unlikely to have backward-drifting clocks at scale.






