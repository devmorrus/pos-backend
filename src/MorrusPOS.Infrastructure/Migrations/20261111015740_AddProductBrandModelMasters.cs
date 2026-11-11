using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBrandModelMasters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BrandId",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModelId",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "product_brands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_brands", x => x.Id);
                    table.UniqueConstraint("AK_product_brands_BusinessId_Id", x => new { x.BusinessId, x.Id });
                    table.ForeignKey(
                        name: "FK_product_brands_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_identifiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentifierType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NormalizedValue = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_identifiers", x => x.Id);
                    table.CheckConstraint("CK_product_identifiers_Type", "\"IdentifierType\" IN ('sku','barcode')");
                    table.ForeignKey(
                        name: "FK_product_identifiers_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_identifiers_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_identifiers_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_models",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_models", x => x.Id);
                    table.UniqueConstraint("AK_product_models_BusinessId_BrandId_Id", x => new { x.BusinessId, x.BrandId, x.Id });
                    table.ForeignKey(
                        name: "FK_product_models_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_models_product_brands_BusinessId_BrandId",
                        columns: x => new { x.BusinessId, x.BrandId },
                        principalTable: "product_brands",
                        principalColumns: new[] { "BusinessId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_products_BusinessId_BrandId",
                table: "products",
                columns: new[] { "BusinessId", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_products_ModelId",
                table: "products",
                column: "ModelId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_ModelBrand",
                table: "products",
                sql: "\"ModelId\" IS NULL OR \"BrandId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_product_brands_BusinessId_NormalizedName",
                table: "product_brands",
                columns: new[] { "BusinessId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_identifiers_BusinessId_IdentifierType_NormalizedVal~",
                table: "product_identifiers",
                columns: new[] { "BusinessId", "IdentifierType", "NormalizedValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_identifiers_BusinessId_ProductId_IdentifierType",
                table: "product_identifiers",
                columns: new[] { "BusinessId", "ProductId", "IdentifierType" },
                unique: true,
                filter: "\"ProductVariantId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_product_identifiers_BusinessId_ProductVariantId_IdentifierT~",
                table: "product_identifiers",
                columns: new[] { "BusinessId", "ProductVariantId", "IdentifierType" },
                unique: true,
                filter: "\"ProductVariantId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_product_identifiers_ProductId",
                table: "product_identifiers",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_identifiers_ProductVariantId",
                table: "product_identifiers",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_product_models_BusinessId_BrandId_NormalizedName",
                table: "product_models",
                columns: new[] { "BusinessId", "BrandId", "NormalizedName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_products_product_brands_BusinessId_BrandId",
                table: "products",
                columns: new[] { "BusinessId", "BrandId" },
                principalTable: "product_brands",
                principalColumns: new[] { "BusinessId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_product_models_ModelId",
                table: "products",
                column: "ModelId",
                principalTable: "product_models",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
                DO $check$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM (
                            SELECT ""BusinessId"", 'sku' AS kind, upper(trim(""Sku"")) AS value FROM products
                            UNION ALL SELECT ""BusinessId"", 'sku', upper(trim(""Sku"")) FROM ""ProductVariants""
                            UNION ALL SELECT ""BusinessId"", 'barcode', trim(""Barcode"") FROM products WHERE ""Barcode"" IS NOT NULL AND trim(""Barcode"") <> ''
                            UNION ALL SELECT ""BusinessId"", 'barcode', trim(""Barcode"") FROM ""ProductVariants"" WHERE ""Barcode"" IS NOT NULL AND trim(""Barcode"") <> ''
                        ) identifiers GROUP BY ""BusinessId"", kind, value HAVING count(*) > 1
                    ) THEN RAISE EXCEPTION 'PRODUCT_IDENTIFIER_CONFLICT: existing SKU/barcode values collide after normalization';
                END IF;
            END $check$;
            INSERT INTO product_identifiers (""Id"", ""BusinessId"", ""IdentifierType"", ""NormalizedValue"", ""ProductId"", ""ProductVariantId"", ""CreatedAt"", ""UpdatedAt"")
            SELECT gen_random_uuid(), ""BusinessId"", 'sku', upper(trim(""Sku"")), ""Id"", NULL, now(), now() FROM products
            UNION ALL
            SELECT gen_random_uuid(), ""BusinessId"", 'barcode', trim(""Barcode""), ""Id"", NULL, now(), now() FROM products WHERE ""Barcode"" IS NOT NULL AND trim(""Barcode"") <> ''
            UNION ALL
            SELECT gen_random_uuid(), ""BusinessId"", 'sku', upper(trim(v.""Sku"")), v.""ProductId"", v.""Id"", now(), now() FROM ""ProductVariants"" v
            UNION ALL
            SELECT gen_random_uuid(), ""BusinessId"", 'barcode', trim(v.""Barcode""), v.""ProductId"", v.""Id"", now(), now() FROM ""ProductVariants"" v WHERE v.""Barcode"" IS NOT NULL AND trim(v.""Barcode"") <> '';

            CREATE OR REPLACE FUNCTION product_identifier_sync() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                DELETE FROM product_identifiers WHERE ""ProductId"" = COALESCE(NEW.""Id"", OLD.""Id"") AND ""ProductVariantId"" IS NULL;
                IF TG_OP <> 'DELETE' THEN
                    INSERT INTO product_identifiers (""Id"", ""BusinessId"", ""IdentifierType"", ""NormalizedValue"", ""ProductId"", ""CreatedAt"", ""UpdatedAt"")
                    VALUES (gen_random_uuid(), NEW.""BusinessId"", 'sku', upper(trim(NEW.""Sku"")), NEW.""Id"", now(), now());
                    IF NEW.""Barcode"" IS NOT NULL AND trim(NEW.""Barcode"") <> '' THEN
                        INSERT INTO product_identifiers (""Id"", ""BusinessId"", ""IdentifierType"", ""NormalizedValue"", ""ProductId"", ""CreatedAt"", ""UpdatedAt"")
                        VALUES (gen_random_uuid(), NEW.""BusinessId"", 'barcode', trim(NEW.""Barcode""), NEW.""Id"", now(), now());
                    END IF;
                END IF; RETURN NEW;
            END $$;
            CREATE OR REPLACE FUNCTION product_variant_identifier_sync() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                DELETE FROM product_identifiers WHERE ""ProductVariantId"" = COALESCE(NEW.""Id"", OLD.""Id"");
                IF TG_OP <> 'DELETE' THEN
                    INSERT INTO product_identifiers (""Id"", ""BusinessId"", ""IdentifierType"", ""NormalizedValue"", ""ProductId"", ""ProductVariantId"", ""CreatedAt"", ""UpdatedAt"")
                    VALUES (gen_random_uuid(), NEW.""BusinessId"", 'sku', upper(trim(NEW.""Sku"")), NEW.""ProductId"", NEW.""Id"", now(), now());
                    IF NEW.""Barcode"" IS NOT NULL AND trim(NEW.""Barcode"") <> '' THEN
                        INSERT INTO product_identifiers (""Id"", ""BusinessId"", ""IdentifierType"", ""NormalizedValue"", ""ProductId"", ""ProductVariantId"", ""CreatedAt"", ""UpdatedAt"")
                        VALUES (gen_random_uuid(), NEW.""BusinessId"", 'barcode', trim(NEW.""Barcode""), NEW.""ProductId"", NEW.""Id"", now(), now());
                    END IF;
                END IF; RETURN NEW;
            END $$;
            CREATE TRIGGER product_identifier_sync_trigger AFTER INSERT OR UPDATE OR DELETE ON products FOR EACH ROW EXECUTE FUNCTION product_identifier_sync();
            CREATE TRIGGER product_variant_identifier_sync_trigger AFTER INSERT OR UPDATE OR DELETE ON ""ProductVariants"" FOR EACH ROW EXECUTE FUNCTION product_variant_identifier_sync();

            ALTER TABLE product_brands ENABLE ROW LEVEL SECURITY; ALTER TABLE product_brands FORCE ROW LEVEL SECURITY;
            ALTER TABLE product_models ENABLE ROW LEVEL SECURITY; ALTER TABLE product_models FORCE ROW LEVEL SECURITY;
            ALTER TABLE product_identifiers ENABLE ROW LEVEL SECURITY; ALTER TABLE product_identifiers FORCE ROW LEVEL SECURITY;
            CREATE POLICY tenant_isolation ON product_brands TO morrus_app USING (""BusinessId""=app_current_business_id()) WITH CHECK (""BusinessId""=app_current_business_id());
            CREATE POLICY tenant_isolation ON product_models TO morrus_app USING (""BusinessId""=app_current_business_id()) WITH CHECK (""BusinessId""=app_current_business_id());
            CREATE POLICY tenant_isolation ON product_identifiers TO morrus_app USING (""BusinessId""=app_current_business_id()) WITH CHECK (""BusinessId""=app_current_business_id());
            GRANT SELECT, INSERT, UPDATE, DELETE ON product_brands, product_models, product_identifiers TO morrus_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_product_brands_BusinessId_BrandId",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_product_models_ModelId",
                table: "products");

            migrationBuilder.DropTable(
                name: "product_identifiers");

            migrationBuilder.DropTable(
                name: "product_models");

            migrationBuilder.DropTable(
                name: "product_brands");

            migrationBuilder.DropIndex(
                name: "IX_products_BusinessId_BrandId",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_ModelId",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_ModelBrand",
                table: "products");

            migrationBuilder.DropColumn(
                name: "BrandId",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ModelId",
                table: "products");
        }
    }
}
