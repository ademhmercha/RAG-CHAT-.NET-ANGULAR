using DocMind.Domain.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace DocMind.Infrastructure.Persistence.Configurations;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public const string SequenceColumn = Application.Features.Conversations.MessageOrdering.Column;

    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.Content).IsRequired();

        builder.OwnsMany(m => m.Citations, citations => citations.ToJson());
        builder.Navigation(m => m.Citations).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsOne(m => m.Usage);

        builder.Property<long>(SequenceColumn).ValueGeneratedOnAdd().UseIdentityAlwaysColumn();

        builder.HasIndex(nameof(Message.ConversationId), SequenceColumn);
    }
}
