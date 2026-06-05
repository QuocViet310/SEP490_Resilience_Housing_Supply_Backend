using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");

        builder.HasKey(x => x.MessageId);

        builder.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(2000);

        // Relationships
        builder.HasOne(x => x.Sender)
            .WithMany(u => u.SentMessages)
            .HasForeignKey(x => x.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Receiver)
            .WithMany(u => u.ReceivedMessages)
            .HasForeignKey(x => x.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => x.SenderId);
        builder.HasIndex(x => x.ReceiverId);
        builder.HasIndex(x => x.SentAt);
        builder.HasIndex(x => new { x.SenderId, x.ReceiverId });
    }
}
