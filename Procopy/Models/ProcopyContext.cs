using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Procopy.Models;

public partial class ProcopyContext : DbContext
{
    public ProcopyContext()
    {
    }

    public ProcopyContext(DbContextOptions<ProcopyContext> options)
        : base(options)
    {
    }

    // --- MEVCUT TABLOLAR ---
    public virtual DbSet<Category> Categories { get; set; }
    public virtual DbSet<Product> Products { get; set; }
    public virtual DbSet<ProductImage> ProductImages { get; set; }
    public virtual DbSet<ProductStat> ProductStats { get; set; }
    public virtual DbSet<ProductCategory> ProductCategories { get; set; }
    public virtual DbSet<ContactMessage> ContactMessages { get; set; }

    // --- YENİ EKLENEN TABLOLAR (Scraping ve Hesaplama İçin) ---
    public virtual DbSet<ProductOption> ProductOptions { get; set; }
    public virtual DbSet<ProductOptionValue> ProductOptionValues { get; set; }
    public DbSet<ImportRun> ImportRuns { get; set; }
    public DbSet<ImportLog> ImportLogs { get; set; }

    // --- MATBAA TABLOLARI ---
    public DbSet<PrintProductType> PrintProductTypes { get; set; }
    public DbSet<PrintParameter> PrintParameters { get; set; }
    public DbSet<PrintParameterValue> PrintParameterValues { get; set; }
    public DbSet<PrintSize> PrintSizes { get; set; }
    public DbSet<PrintQuantity> PrintQuantities { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning bdTo protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=.;Database=Procopy;Integrated Security=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Categori__19093A2B16DD838E");

            entity.Property(e => e.CategoryId).HasColumnName("CategoryID");
            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ImageUrl).HasMaxLength(250);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ParentId).HasColumnName("ParentID");
            entity.Property(e => e.Slug).HasMaxLength(150);

           entity.HasOne(d => d.Parent).WithMany(p => p.Children)
                .HasForeignKey(d => d.ParentId)
                .HasConstraintName("FK__Categorie__Paren__398D8EEE");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK__Products__B40CC6EDF257F168");

            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.CategoryId).HasColumnName("CategoryID");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsFeatured).HasDefaultValue(false);
            entity.Property(e => e.MainImageUrl).HasMaxLength(250);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OldPrice).HasColumnType("decimal(18, 2)"); // Bunu da ekledim garanti olsun
            entity.Property(e => e.ProductCode).HasMaxLength(50);
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.Slug).HasMaxLength(250);
            entity.Property(e => e.SourceUrl).HasMaxLength(500); // Scraping Linki İçin

            /* entity.HasOne(d => d.Category).WithMany(p => p.Products)
                 .HasForeignKey(d => d.CategoryId)
                 .OnDelete(DeleteBehavior.ClientSetNull)
                 .HasConstraintName("FK__Products__Catego__3F466844");*/
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.ImageId).HasName("PK__ProductI__7516F4ECBF7B2B7E");

            entity.Property(e => e.ImageId).HasColumnName("ImageID");
            entity.Property(e => e.ImageUrl).HasMaxLength(250);
            entity.Property(e => e.IsMain).HasDefaultValue(false);
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.SortOrder).HasDefaultValue(0);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ProductIm__Produ__440B1D61");
        });

        modelBuilder.Entity<ProductStat>(entity =>
        {
            entity.HasKey(e => e.StatId).HasName("PK__ProductS__3A162D1E78BDA332");

            entity.Property(e => e.StatId).HasColumnName("StatID");
            entity.Property(e => e.InteractionDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Ipaddress)
                .HasMaxLength(50)
                .HasColumnName("IPAddress");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductStats)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ProductSt__Produ__47DBAE45");
        });

        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.CategoryId });

            entity.HasOne(d => d.Product)
                .WithMany(p => p.ProductCategories)
                .HasForeignKey(d => d.ProductId);

            entity.HasOne(d => d.Category)
                .WithMany(p => p.ProductCategories)
                .HasForeignKey(d => d.CategoryId);
        });

        // --- YENİ EKLENEN İLİŞKİLER ---

        // ProductOption Tablosu
        modelBuilder.Entity<ProductOption>(entity =>
        {
            entity.HasKey(e => e.OptionId);

            // Bir Seçenek, Bir Ürüne Aittir
            entity.HasOne(d => d.Product)
                .WithMany(p => p.ProductOptions)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade); // Ürün silinirse seçenekleri de silinsin
        });

        // ProductOptionValue Tablosu
        modelBuilder.Entity<ProductOptionValue>(entity =>
        {
            entity.HasKey(e => e.ValueId);

            // Bir Değer, Bir Seçeneğe Aittir (Örn: "A4", "Ebat" seçeneğine aittir)
            entity.HasOne(d => d.Option)
                .WithMany(p => p.Values)
                .HasForeignKey(d => d.OptionId)
                .OnDelete(DeleteBehavior.Cascade); // Seçenek silinirse değerleri de silinsin

            entity.Property(e => e.PriceAdjustment).HasColumnType("decimal(18, 2)");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}