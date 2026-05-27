.PHONY: up down clean logs demo

up:
	docker compose up -d

down:
	docker compose down

clean:
	docker compose down -v

logs:
	docker compose logs -f --tail=50

demo:
	@echo "Producing 5 mixed-priority test OTPs..."
	@dotnet run --project tools/OtpService.LoadTest --no-build -- --rate 5 --duration 1 --mode mixed-priority
	@echo ""
	@echo "Waiting 5 seconds for the pipeline to process..."
	@sleep 5
	@echo ""
	@echo "=== Recent OTP records in Postgres ==="
	@docker compose exec postgres psql -U otp -d otp -c "SELECT request_id, purpose, priority, status FROM otp_records WHERE request_id LIKE 'load-%' ORDER BY created_at DESC LIMIT 5;"
