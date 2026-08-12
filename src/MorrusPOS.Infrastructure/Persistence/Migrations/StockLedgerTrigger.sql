-- Trigger database untuk update inventory_stock.qty_on_hand otomatis
-- setiap kali ada baris baru masuk ke stock_ledger.
--
-- CATATAN:
-- Versi production sekarang sudah diformalisasi lewat migration
-- 20260812093000_FixInventoryStockTrigger. File ini dipertahankan sebagai
-- referensi SQL manual, dan harus selalu identik dengan body migration itu.

CREATE OR REPLACE FUNCTION fn_update_inventory_stock()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO "inventory_stock" ("Id", "ProductId", "OutletId", "QtyOnHand", "MinStockAlert", "UpdatedAt")
    VALUES (gen_random_uuid(), NEW."ProductId", NEW."OutletId", NEW."QtyChange", 0, now())
    ON CONFLICT ("ProductId", "OutletId")
    DO UPDATE SET
        "QtyOnHand" = "inventory_stock"."QtyOnHand" + NEW."QtyChange",
        "UpdatedAt" = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_stock_ledger_insert ON "stock_ledger";

CREATE TRIGGER trg_stock_ledger_insert
AFTER INSERT ON "stock_ledger"
FOR EACH ROW
EXECUTE FUNCTION fn_update_inventory_stock();


-- CATATAN: constraint unique (product_id, outlet_id) di inventory_stock
-- WAJIB ada sebelum trigger ini dipasang — sudah didefinisikan lewat
-- HasIndex(...).IsUnique() di InventoryStockConfiguration.cs.
