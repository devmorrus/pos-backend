\set ON_ERROR_STOP on

SELECT "ImageUrl" FROM products
WHERE "ImageUrl" LIKE '/uploads/%'
UNION ALL
SELECT "ImageUrl" FROM "ProductVariants"
WHERE "ImageUrl" LIKE '/uploads/%'
UNION ALL
SELECT "AttachmentUrl" FROM cash_flows
WHERE "AttachmentUrl" LIKE '/uploads/%';
