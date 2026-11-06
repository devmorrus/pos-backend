# Runbook: Stock Integrity Error

1. Ambil `TraceId`, `BusinessId`, `OutletId`, dan `error.code` dari log; jangan salin payload customer.
2. Cari sale ledger dan transaction terkait berdasarkan trace/reference ID.
3. Jalankan reconciliation preview, bukan repair, untuk outlet terdampak.
4. Pastikan saldo ledger, inventory projection, batch, dan serial konsisten.
5. Repair hanya setelah penyebab ditemukan dan dengan approval operasional.
6. Jangan mengubah `inventory_stock` atau `stock_ledger` langsung.
