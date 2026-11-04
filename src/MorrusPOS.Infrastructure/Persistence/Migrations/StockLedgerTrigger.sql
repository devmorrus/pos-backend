-- Trigger database untuk update inventory_stock.qty_on_hand otomatis
-- setiap kali ada baris baru masuk ke stock_ledger.
--
-- CATATAN:
-- Versi production sekarang diformalisasi lewat migration
-- 20261102055719_CompleteTenantIsolation. File ini dipertahankan sebagai
-- referensi SQL manual dan harus identik dengan body migration tersebut.

CREATE OR REPLACE FUNCTION fn_update_inventory_stock()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path=pg_catalog,public
AS $$
BEGIN
    PERFORM pg_advisory_xact_lock_shared(
        hashtextextended(NEW."BusinessId"::text || ':' || NEW."OutletId"::text, 0));

    IF NOT EXISTS (
        SELECT 1 FROM public.products p
        WHERE p."BusinessId"=NEW."BusinessId"
          AND p."Id"=NEW."ProductId"
          AND p."TrackingMode"<>'none')
    THEN
        RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: produk tidak valid atau tidak memiliki stok';
    END IF;

    IF NEW."ProductVariantId" IS NOT NULL THEN
        INSERT INTO public.inventory_stock
            ("Id", "BusinessId", "ProductId", "ProductVariantId", "OutletId", "QtyOnHand", "MinStockAlert", "UpdatedAt")
        VALUES
            (gen_random_uuid(), NEW."BusinessId", NEW."ProductId", NEW."ProductVariantId", NEW."OutletId", NEW."QtyChange", 0, now())
        ON CONFLICT ("BusinessId", "ProductId", "ProductVariantId", "OutletId")
            WHERE "ProductVariantId" IS NOT NULL
        DO UPDATE SET
            "QtyOnHand" = public.inventory_stock."QtyOnHand" + NEW."QtyChange",
            "UpdatedAt" = now();
    ELSE
        INSERT INTO public.inventory_stock
            ("Id", "BusinessId", "ProductId", "OutletId", "QtyOnHand", "MinStockAlert", "UpdatedAt")
        VALUES
            (gen_random_uuid(), NEW."BusinessId", NEW."ProductId", NEW."OutletId", NEW."QtyChange", 0, now())
        ON CONFLICT ("BusinessId", "ProductId", "OutletId")
            WHERE "ProductVariantId" IS NULL
        DO UPDATE SET
            "QtyOnHand" = public.inventory_stock."QtyOnHand" + NEW."QtyChange",
            "UpdatedAt" = now();
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_stock_ledger_insert ON "stock_ledger";

CREATE TRIGGER trg_stock_ledger_insert
AFTER INSERT ON "stock_ledger"
FOR EACH ROW
EXECUTE FUNCTION fn_update_inventory_stock();


-- CATATAN: constraint unique tenant-aware di inventory_stock
-- WAJIB ada sebelum trigger ini dipasang — sudah didefinisikan lewat
-- HasIndex(...).IsUnique() di InventoryStockConfiguration.cs.
