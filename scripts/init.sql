-- 启用 pgvector 扩展
CREATE EXTENSION IF NOT EXISTS vector;

-- 启用全文搜索扩展
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- 创建数据库（如果不存在）
SELECT 'CREATE DATABASE qiakon'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'qiakon')\gexec
