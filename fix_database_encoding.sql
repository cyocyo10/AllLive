-- ============================================
-- 数据库乱码修复 SQL 脚本
-- ============================================
-- 说明：此脚本用于修复数据库中的中文乱码问题
-- 使用方法：
--   1. 找到数据库文件：%LocalAppData%\Packages\<应用包名>\LocalState\alllive.db
--   2. 使用 SQLite 工具（如 DB Browser for SQLite）打开数据库
--   3. 执行此脚本
-- ============================================

-- 设置数据库编码为 UTF-8
PRAGMA encoding = 'UTF-8';

-- ============================================
-- 方案1：修复站点名称（尝试映射乱码到正确名称）
-- ============================================

-- 修复收藏表中的乱码站点名称
UPDATE Favorite 
SET site_name = '哔哩哔哩直播' 
WHERE site_name LIKE '%±%' OR site_name LIKE '%¹%' OR site_name LIKE '%Ö±%' OR site_name LIKE 'bilibili%';

UPDATE Favorite 
SET site_name = '斗鱼直播' 
WHERE site_name LIKE '%¶·%' OR site_name LIKE '%Óã%' OR site_name LIKE 'douyu%';

UPDATE Favorite 
SET site_name = '虎牙直播' 
WHERE site_name LIKE '%»¢%' OR site_name LIKE '%ÑÀ%' OR site_name LIKE 'huya%';

UPDATE Favorite 
SET site_name = '抖音直播' 
WHERE site_name LIKE '%¶¶%' OR site_name LIKE '%Òô%' OR site_name LIKE 'douyin%';

-- 修复历史表中的乱码站点名称
UPDATE History 
SET site_name = '哔哩哔哩直播' 
WHERE site_name LIKE '%±%' OR site_name LIKE '%¹%' OR site_name LIKE '%Ö±%' OR site_name LIKE 'bilibili%';

UPDATE History 
SET site_name = '斗鱼直播' 
WHERE site_name LIKE '%¶·%' OR site_name LIKE '%Óã%' OR site_name LIKE 'douyu%';

UPDATE History 
SET site_name = '虎牙直播' 
WHERE site_name LIKE '%»¢%' OR site_name LIKE '%ÑÀ%' OR site_name LIKE 'huya%';

UPDATE History 
SET site_name = '抖音直播' 
WHERE site_name LIKE '%¶¶%' OR site_name LIKE '%Òô%' OR site_name LIKE 'douyin%';

-- ============================================
-- 方案2：删除无法修复的乱码数据
-- ============================================

-- 删除收藏表中仍然是乱码的数据
DELETE FROM Favorite 
WHERE site_name NOT IN ('哔哩哔哩直播', '斗鱼直播', '虎牙直播', '抖音直播');

-- 删除历史表中仍然是乱码的数据
DELETE FROM History 
WHERE site_name NOT IN ('哔哩哔哩直播', '斗鱼直播', '虎牙直播', '抖音直播');

-- ============================================
-- 验证修复结果
-- ============================================

-- 查看收藏表中的站点分布
SELECT site_name, COUNT(*) as count 
FROM Favorite 
GROUP BY site_name;

-- 查看历史表中的站点分布
SELECT site_name, COUNT(*) as count 
FROM History 
GROUP BY site_name;

-- 查看是否还有乱码数据
SELECT '收藏表乱码数据' as table_name, COUNT(*) as corrupted_count
FROM Favorite 
WHERE site_name NOT IN ('哔哩哔哩直播', '斗鱼直播', '虎牙直播', '抖音直播')
UNION ALL
SELECT '历史表乱码数据' as table_name, COUNT(*) as corrupted_count
FROM History 
WHERE site_name NOT IN ('哔哩哔哩直播', '斗鱼直播', '虎牙直播', '抖音直播');
