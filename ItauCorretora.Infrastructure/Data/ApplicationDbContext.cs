using Microsoft.EntityFrameworkCore;
using ItauCorretora.Domain.Entities;

namespace ItauCorretora.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Custodia> Custodias { get; set; }
        public DbSet<OrdemCompra> OrdensCompra { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Mapeamento do Cliente
            modelBuilder.Entity<Cliente>(builder =>
            {
                builder.ToTable("Clientes");
                builder.HasKey(c => c.Id);
                builder.Property(c => c.Nome).IsRequired().HasMaxLength(100);
                builder.Property(c => c.Cpf).IsRequired().HasMaxLength(14);
                builder.Property(c => c.ContaFilhote).IsRequired().HasMaxLength(20);
                builder.Property(c => c.AporteMensal).HasPrecision(18, 2);
                builder.Property(c => c.SaldoResidual).HasPrecision(18, 2);
                
                builder.Metadata.FindNavigation(nameof(Cliente.Custodias))
                       ?.SetPropertyAccessMode(PropertyAccessMode.Field);
            });

            // Mapeamento da Custodia
            modelBuilder.Entity<Custodia>(builder =>
            {
                builder.ToTable("Custodias");
                builder.HasKey(c => c.Id);
                builder.Property(c => c.Ticker).IsRequired().HasMaxLength(10);
                builder.Property(c => c.PrecoMedio).HasPrecision(18, 6);
                
                builder.HasOne<Cliente>()
                       .WithMany(c => c.Custodias)
                       .HasForeignKey(c => c.ClienteId)
                       .OnDelete(DeleteBehavior.Cascade);
            });

            // NOVO: Mapeamento da OrdemCompra e RateioOrdem
            modelBuilder.Entity<OrdemCompra>(builder =>
            {
                builder.ToTable("OrdensCompra");
                builder.HasKey(o => o.Id);
                builder.Property(o => o.Ticker).IsRequired().HasMaxLength(10);
                builder.Property(o => o.PrecoUnitario).HasPrecision(18, 2);
                builder.Property(o => o.ValorTotalOrdem).HasPrecision(18, 2);

                // Configura o Rateio como uma tabela "Filha" (Owned Entity)
                // Isso resolve o erro de Primary Key (PK) que o terminal acusou
                builder.OwnsMany(o => o.Rateios, x =>
                {
                    x.ToTable("RateiosOrdem");
                    x.WithOwner().HasForeignKey("OrdemCompraId");
                    x.Property<Guid>("Id"); // Criamos uma PK de sombra no banco
                    x.HasKey("Id");
                    x.Property(r => r.ContaFilhote).IsRequired().HasMaxLength(20);
                    x.Property(r => r.ValorFinanceiroRateio).HasPrecision(18, 2);
                    x.Property(r => r.ResidualCliente).HasPrecision(18, 2);
                    x.Property(r => r.ValorIrDedoDuro).HasPrecision(18, 4);
                });
            });
        }
    }
}