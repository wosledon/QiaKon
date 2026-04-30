# QiaKon KAG Platform

> **Knowledge Answer Graph** - 企业级知识问答图谱平台

将知识图谱的结构化推理能力与 RAG 的灵活检索能力深度融合，提供准确、可信、可溯源的智能问答能力。

---

## 🌟 核心价值

| 价值点         | 描述                             |
| -------------- | -------------------------------- |
| **知识可信**   | 基于知识图谱，答案可溯源、可审计 |
| **推理可解释** | 显式推理链路，决策过程透明       |
| **检索精准**   | 混合检索（向量+关键词+图谱关系） |
| **领域适配**   | MoE 分块策略，适配不同领域       |
| **工程可扩展** | 模块化架构，连接器模式           |

---

## 🚀 快速开始

### 1. 配置环境
```bash
cp .env.example .env
# 编辑 .env 填写连接信息
```

### 2. 运行后端
```bash
dotnet build QiaKon.slnx
dotnet run --project src/QiaKon.Api
```

### 3. 运行前端
```bash
cd frontend
npm install
npm run dev
```

---

## 📊 项目状态

✅ **全部 Phase 完成**
- 后端：30+ 模块，0 编译错误
- 前端：React 19 + Vite 7 + Tailwind CSS 4
- 测试：核心模块覆盖

---

## 📁 文档

- [AGENTS.md](AGENTS.md) - 多 AI 协同规范
- [docs/PRD.md](docs/PRD.md) - 产品需求
- [docs/FSD.md](docs/FSD.md) - 功能规格
- [docs/FUNC.md](docs/FUNC.md) - 功能清单

---

**最后更新**: 2026-04-28

