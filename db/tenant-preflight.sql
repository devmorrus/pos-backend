-- Read-only legacy data audit. Resolve every reported row before applying RLS.

SELECT 'businesses_without_owner' AS check_name, COUNT(*) AS remaining
FROM businesses WHERE "OwnerId" IS NULL;

SELECT 'outlets_without_business' AS check_name, COUNT(*) AS remaining
FROM outlets WHERE "BusinessId" IS NULL;

SELECT 'users_without_business' AS check_name, COUNT(*) AS remaining
FROM users WHERE "BusinessId" IS NULL;

SELECT 'products_without_business' AS check_name, COUNT(*) AS remaining
FROM products WHERE "BusinessId" IS NULL;

SELECT 'categories_without_business' AS check_name, COUNT(*) AS remaining
FROM categories WHERE "BusinessId" IS NULL;

SELECT 'suppliers_without_business' AS check_name, COUNT(*) AS remaining
FROM suppliers WHERE "BusinessId" IS NULL;

SELECT 'cross_tenant_product_category' AS check_name, COUNT(*) AS remaining
FROM products p
JOIN categories c ON c."Id" = p."CategoryId"
WHERE p."BusinessId" IS NOT NULL
  AND c."BusinessId" IS NOT NULL
  AND p."BusinessId" <> c."BusinessId";
