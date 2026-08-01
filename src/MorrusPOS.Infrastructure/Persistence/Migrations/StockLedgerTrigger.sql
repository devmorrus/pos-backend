-- Trigger database untuk update inventory_stock.qty_on_hand otomatis
-- setiap kali ada baris baru masuk ke stock_ledger.
--
-- CARA PAKAI: setelah migration "InitialCreate" berhasil di-apply, jalankan
-- file ini secara manual sekali (psql -f StockLedgerTrigger.sql), ATAU
-- masukkan isi file ini ke migrationBuilder.Sql(...) di migration terpisah:
--
--   dotnet ef migrations add AddStockLedgerTrigger
--
-- lalu tempel isi file ini ke method Up() migration tersebut.

CREATE OR REPLACE FUNCTION fn_update_inventory_stock()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO inventory_stock (id, product_id, outlet_id, qty_on_hand, min_stock_alert, updated_at)
    VALUES (gen_random_uuid(), NEW.product_id, NEW.outlet_id, NEW.qty_change, 0, now())
    ON CONFLICT (product_id, outlet_id)
    DO UPDATE SET
        qty_on_hand = inventory_stock.qty_on_hand + NEW.qty_change,
        updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_stock_ledger_insert ON stock_ledger;

CREATE TRIGGER trg_stock_ledger_insert
AFTER INSERT ON stock_ledger
FOR EACH ROW
EXECUTE FUNCTION fn_update_inventory_stock();

-- CATATAN: constraint unique (product_id, outlet_id) di inventory_stock
-- WAJIB ada sebelum trigger ini dipasang — sudah didefinisikan lewat
-- HasIndex(...).IsUnique() di InventoryStockConfiguration.cs.
