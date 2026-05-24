using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OtpService.Core.Models;

namespace OtpService.Infrastructure.Persistence.Configurations;

public sealed class OtpRecordConfiguration : IEntityTypeConfiguration<OtpRecord>
{
    public void Configure(EntityTypeBuilder<OtpRecord> builder)
    {
        builder.ToTable("otp_records");
        builder.HasKey(x => x.Id);

        // Optimistic concurrency via Postgres xmin system column 
        // Catches racing UPDATEs (e.g. two verify requests on the same OTP)
        // by throwing DbUpdateConcurrencyException instead of silently overwriting.
        builder
            .Property<uint>("xmin")
            .IsRowVersion()
            .Metadata.SetColumnName("xmin");
        // ─── Identity columns ────────────────────────────────────────
        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.TrackingId)
            .HasColumnName("tracking_id");

        builder.Property(x => x.RequestId)
            .HasColumnName("request_id")
            .HasMaxLength(64)           
            .IsRequired();

        builder.Property(x => x.Msisdn)
            .HasColumnName("msisdn")
            .HasMaxLength(20)          
            .IsRequired();

        builder.Property(x => x.Purpose)
            .HasColumnName("purpose")
            .HasMaxLength(32)
            .IsRequired();

      
        builder.Property(x => x.Priority)
            .HasColumnName("priority")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

       
        builder.Property(x => x.CodeHash)
            .HasColumnName("code_hash")
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(x => x.Salt)
            .HasColumnName("salt")
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(x => x.Attempts)
            .HasColumnName("attempts")
            .HasDefaultValue(0);

        builder.Property(x => x.MaxAttempts)
            .HasColumnName("max_attempts")
            .HasDefaultValue(5);

        builder.Property(x => x.WhatsAppMessageId)
            .HasColumnName("whatsapp_message_id")
            .HasMaxLength(128);

        builder.Property(x => x.LastAppliedTimestamp)
            .HasColumnName("last_applied_timestamp");

        builder.Property(x => x.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(256);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");  

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.VerifiedAt)
            .HasColumnName("verified_at")
            .HasColumnType("timestamptz");

      
        builder.HasIndex(x => x.TrackingId)
            .IsUnique()
            .HasDatabaseName("ix_otp_records_tracking_id");

        
        builder.HasIndex(x => x.RequestId)
            .IsUnique()
            .HasDatabaseName("ix_otp_records_request_id");

        builder.HasIndex(x => x.Msisdn)
            .HasDatabaseName("ix_otp_records_msisdn");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_otp_records_status");

        builder.HasIndex(x => x.WhatsAppMessageId)
            .HasDatabaseName("ix_otp_records_whatsapp_message_id");
    }
}