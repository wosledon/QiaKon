-- QiaKon 初始化脚本
-- 用途：在 PostgreSQL 已创建 qiakon 数据库后，初始化扩展、表结构和基础种子数据。

-- 启用扩展
CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- =========================
-- 文档表
-- =========================
CREATE TABLE IF NOT EXISTS documents
(
	id uuid PRIMARY KEY,
	title varchar(300) NOT NULL,
	content text NULL,
	type integer NOT NULL,
	department_id uuid NOT NULL,
	access_level integer NOT NULL,
	index_status integer NOT NULL,
	version integer NOT NULL,
	index_version integer NULL,
	metadata_json text NULL,
	size bigint NOT NULL,
	created_by uuid NOT NULL,
	created_at timestamptz NOT NULL,
	modified_by uuid NULL,
	modified_at timestamptz NULL,
	file_path text NULL,
	index_progress double precision NULL,
	index_started_at timestamptz NULL,
	index_completed_at timestamptz NULL,
	index_error_message text NULL
);

CREATE INDEX IF NOT EXISTS ix_documents_department_id ON documents (department_id);
CREATE INDEX IF NOT EXISTS ix_documents_index_status ON documents (index_status);
CREATE INDEX IF NOT EXISTS ix_documents_created_at ON documents (created_at DESC);

CREATE TABLE IF NOT EXISTS document_chunks
(
	id uuid PRIMARY KEY,
	document_id uuid NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
	content text NOT NULL,
	"order" integer NOT NULL,
	chunking_strategy varchar(128) NULL,
	created_at timestamptz NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_document_chunks_document_order ON document_chunks (document_id, "order");

-- =========================
-- 图谱表
-- =========================
CREATE TABLE IF NOT EXISTS graph_entities
(
	id varchar(128) PRIMARY KEY,
	name varchar(256) NOT NULL,
	type varchar(128) NOT NULL,
	department_id uuid NOT NULL,
	is_public boolean NOT NULL,
	properties_json text NULL,
	created_by uuid NOT NULL,
	created_at timestamptz NOT NULL,
	updated_at timestamptz NULL
);

CREATE INDEX IF NOT EXISTS ix_graph_entities_type ON graph_entities (type);
CREATE INDEX IF NOT EXISTS ix_graph_entities_department_id ON graph_entities (department_id);
CREATE INDEX IF NOT EXISTS ix_graph_entities_is_public ON graph_entities (is_public);

CREATE TABLE IF NOT EXISTS graph_relations
(
	id varchar(128) PRIMARY KEY,
	source_id varchar(128) NOT NULL REFERENCES graph_entities(id) ON DELETE RESTRICT,
	target_id varchar(128) NOT NULL REFERENCES graph_entities(id) ON DELETE RESTRICT,
	type varchar(128) NOT NULL,
	department_id uuid NOT NULL,
	properties_json text NULL,
	created_by uuid NOT NULL,
	created_at timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_graph_relations_source_id ON graph_relations (source_id);
CREATE INDEX IF NOT EXISTS ix_graph_relations_target_id ON graph_relations (target_id);
CREATE INDEX IF NOT EXISTS ix_graph_relations_type ON graph_relations (type);

-- =========================
-- 文档种子数据
-- type: 0=PlainText 1=Markdown 2=Html 3=Pdf 4=Word 5=Table
-- access_level: 0=Public 1=Department 2=Restricted 3=Confidential
-- index_status: 0=Pending 1=Indexing 2=Completed 3=Failed
-- =========================
INSERT INTO documents (id, title, content, type, department_id, access_level, index_status, version, index_version, metadata_json, size, created_by, created_at, file_path, index_progress, index_started_at, index_completed_at, index_error_message)
VALUES
	('d1111111-1111-1111-1111-111111111111', 'QiaKon平台架构设计文档', 'QiaKon是一个企业级KAG平台，将知识图谱的结构化推理能力与RAG的灵活检索能力深度融合。平台采用模块化架构，包括API层、服务层、数据层等多个组件。核心技术栈包括：.NET 9、ASP.NET Core、EF Core、PostgreSQL、Redis等。', 1, '22222222-2222-2222-2222-222222222222', 1, 2, 1, 1, '{"author":"系统管理员","tags":["架构","设计"]}', 4096, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '30 day', NULL, 100, NULL, now() - interval '1 day', NULL),
	('d2222222-2222-2222-2222-222222222222', 'RAG检索管道技术方案', 'RAG（检索增强生成）管道包含以下关键组件：文档解析器支持PDF、Word、Markdown等格式；分块策略支持语义分块、递归分块、固定长度分块；嵌入服务生成文档块的向量表示；向量存储使用pgvector。', 1, '22222222-2222-2222-2222-222222222222', 3, 2, 2, 1, '{"author":"工程师","tags":["RAG","检索"]}', 3584, 'dddddddd-dddd-dddd-dddd-dddddddddddd', now() - interval '20 day', NULL, 100, NULL, now() - interval '1 day', NULL),
	('d3333333-3333-3333-3333-333333333333', '知识图谱引擎设计文档', '知识图谱引擎支持内存与Npgsql两种存储后端，提供实体管理、关系管理、路径查询、多跳推理等能力。实体属性支持JSON格式。', 1, '22222222-2222-2222-2222-222222222222', 0, 2, 1, 1, '{"author":"系统管理员","tags":["图谱","引擎"]}', 2048, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '15 day', NULL, 100, NULL, now() - interval '1 day', NULL),
	('d4444444-4444-4444-4444-444444444444', '公司年度销售报告2025', '2025年度销售报告：全年销售额同比增长25%，达到5亿元人民币。新增客户200家，重点行业突破包括金融、医疗、制造三大领域。', 3, '33333333-3333-3333-3333-333333333333', 2, 2, 1, 1, '{"author":"销售部","department":"销售部"}', 5120, 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', now() - interval '10 day', NULL, 100, NULL, now() - interval '1 day', NULL),
	('d5555555-5555-5555-5555-555555555555', '员工手册', '欢迎加入QiaKon公司！本手册包含公司制度、福利政策、考勤规定等内容。所有员工入职后需完成岗前培训。', 1, '44444444-4444-4444-4444-444444444444', 0, 0, 1, NULL, '{"author":"人力资源部"}', 1536, 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', now() - interval '5 day', NULL, 0, NULL, NULL, NULL),
	('d6666666-6666-6666-6666-666666666666', '研发部项目管理制度', '研发部项目管理规范：代码必须经过review才能合并；单元测试覆盖率需达到80%以上；重要功能必须编写技术文档；发布流程遵循语义化版本规范。', 1, '22222222-2222-2222-2222-222222222222', 1, 1, 3, 2, '{"author":"工程师","tags":["管理","制度"]}', 1920, 'dddddddd-dddd-dddd-dddd-dddddddddddd', now() - interval '2 day', NULL, 50, now() - interval '5 minute', NULL, NULL),
	('d7777777-7777-7777-7777-777777777777', '市场推广方案2025', '2025年市场推广方案包括线上渠道扩展、线下活动策划、品牌合作等内容。重点投入数字营销领域。', 4, '33333333-3333-3333-3333-333333333333', 1, 3, 1, NULL, '{"author":"市场部"}', 2816, 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', now() - interval '1 day', NULL, 0, now() - interval '12 hour', NULL, '模拟解析器缺少Word依赖'),
	('d8888888-8888-8888-8888-888888888888', '新产品功能规划', '下一代产品功能规划：增强型知识推理、多模态检索、高级可视化分析、自动化工作流等核心功能。', 1, '22222222-2222-2222-2222-222222222222', 2, 0, 1, NULL, '{"author":"产品经理","tags":["产品","规划"]}', 1792, 'dddddddd-dddd-dddd-dddd-dddddddddddd', now() - interval '6 hour', NULL, 0, NULL, NULL, NULL)
ON CONFLICT (id) DO NOTHING;

INSERT INTO document_chunks (id, document_id, content, "order", chunking_strategy, created_at)
VALUES
	('c1111111-1111-1111-1111-111111111111', 'd1111111-1111-1111-1111-111111111111', 'QiaKon是一个企业级KAG平台，将知识图谱的结构化推理能力与RAG的灵活检索能力深度融合', 1, 'RecursiveCharacterTextSplitter', now()),
	('c1111112-1111-1111-1111-111111111112', 'd1111111-1111-1111-1111-111111111111', '平台采用模块化架构，包括API层、服务层、数据层等多个组件', 2, 'RecursiveCharacterTextSplitter', now()),
	('c1111113-1111-1111-1111-111111111113', 'd1111111-1111-1111-1111-111111111111', '核心技术栈包括：.NET 9、ASP.NET Core、EF Core、PostgreSQL、Redis等', 3, 'RecursiveCharacterTextSplitter', now()),
	('c2222221-2222-2222-2222-222222222221', 'd2222222-2222-2222-2222-222222222222', 'RAG（检索增强生成）管道包含以下关键组件：文档解析器支持PDF、Word、Markdown等格式', 1, 'RecursiveCharacterTextSplitter', now()),
	('c2222222-2222-2222-2222-222222222222', 'd2222222-2222-2222-2222-222222222222', '分块策略支持语义分块、递归分块、固定长度分块；嵌入服务生成文档块的向量表示；向量存储使用pgvector', 2, 'RecursiveCharacterTextSplitter', now()),
	('c3333331-3333-3333-3333-333333333331', 'd3333333-3333-3333-3333-333333333333', '知识图谱引擎支持内存与Npgsql两种存储后端，提供实体管理、关系管理、路径查询、多跳推理等能力', 1, 'RecursiveCharacterTextSplitter', now()),
	('c3333332-3333-3333-3333-333333333332', 'd3333333-3333-3333-3333-333333333333', '实体属性支持JSON格式', 2, 'RecursiveCharacterTextSplitter', now())
ON CONFLICT (id) DO NOTHING;

-- =========================
-- 图谱种子数据
-- =========================
INSERT INTO graph_entities (id, name, type, department_id, is_public, properties_json, created_by, created_at, updated_at)
VALUES
	('entity_001', 'QiaKon平台', 'Platform', '22222222-2222-2222-2222-222222222222', true, '{"description":"企业级KAG平台","version":"1.0"}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '30 day', NULL),
	('entity_002', 'RAG检索模块', 'Module', '22222222-2222-2222-2222-222222222222', true, '{"description":"检索增强生成模块","technology":"pgvector"}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '25 day', NULL),
	('entity_003', '知识图谱引擎', 'Module', '22222222-2222-2222-2222-222222222222', true, '{"description":"知识图谱存储与查询引擎","storage":"Memory/Npgsql"}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '25 day', NULL),
	('entity_004', '.NET 9', 'Technology', '22222222-2222-2222-2222-222222222222', true, '{"company":"Microsoft"}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '20 day', NULL),
	('entity_005', 'PostgreSQL', 'Database', '22222222-2222-2222-2222-222222222222', true, '{"features":"pgvector"}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '20 day', NULL),
	('entity_006', 'Redis', 'Cache', '22222222-2222-2222-2222-222222222222', false, '{"description":"分布式缓存"}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '15 day', NULL),
	('entity_007', '张伟', 'Person', '22222222-2222-2222-2222-222222222222', false, '{"title":"研发经理","email":"zhangwei@qiakon.com"}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '10 day', NULL),
	('entity_008', '李娜', 'Person', '33333333-3333-3333-3333-333333333333', false, '{"title":"销售总监","email":"lina@qiakon.com"}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '10 day', NULL),
	('entity_009', 'KAG融合架构', 'Concept', '22222222-2222-2222-2222-222222222222', true, '{"description":"知识图谱与RAG深度融合架构"}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '5 day', NULL),
	('entity_010', '向量检索', 'Technology', '22222222-2222-2222-2222-222222222222', true, '{"description":"基于向量相似度的检索技术"}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '5 day', NULL)
ON CONFLICT (id) DO NOTHING;

INSERT INTO graph_relations (id, source_id, target_id, type, department_id, properties_json, created_by, created_at)
VALUES
	('rel_001', 'entity_001', 'entity_002', 'CONTAINS', '22222222-2222-2222-2222-222222222222', '{}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '25 day'),
	('rel_002', 'entity_001', 'entity_003', 'CONTAINS', '22222222-2222-2222-2222-222222222222', '{}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '25 day'),
	('rel_003', 'entity_001', 'entity_009', 'IMPLEMENTS', '22222222-2222-2222-2222-222222222222', '{}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '5 day'),
	('rel_004', 'entity_002', 'entity_010', 'USES', '22222222-2222-2222-2222-222222222222', '{}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '5 day'),
	('rel_005', 'entity_002', 'entity_005', 'USES', '22222222-2222-2222-2222-222222222222', '{}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '20 day'),
	('rel_006', 'entity_003', 'entity_005', 'USES', '22222222-2222-2222-2222-222222222222', '{}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '20 day'),
	('rel_007', 'entity_001', 'entity_004', 'BUILT_WITH', '22222222-2222-2222-2222-222222222222', '{}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '20 day'),
	('rel_008', 'entity_001', 'entity_006', 'USES', '22222222-2222-2222-2222-222222222222', '{}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '15 day'),
	('rel_009', 'entity_007', 'entity_001', 'MANAGES', '22222222-2222-2222-2222-222222222222', '{}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '10 day'),
	('rel_010', 'entity_008', 'entity_001', 'SUPPORTS', '33333333-3333-3333-3333-333333333333', '{}', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now() - interval '10 day')
ON CONFLICT (id) DO NOTHING;
