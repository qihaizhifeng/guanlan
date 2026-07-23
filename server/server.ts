import express from 'express'
import cors from 'cors'
import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'fs'
import { join, dirname } from 'path'
import { fileURLToPath } from 'url'
import { v4 as uuidv4 } from 'uuid'
import multer from 'multer'

const __dirname = dirname(fileURLToPath(import.meta.url))
const DIST_PATH = join(__dirname, '..', 'dist')
const ADMIN_PATH = join(__dirname, '..', 'admin', 'index.html')
const DATA_DIR = process.env.DATA_DIR || __dirname
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD || 'guanlan2024'

const ARTICLES_PATH = join(DATA_DIR, 'articles.json')
const UPLOADS_PATH = join(DATA_DIR, 'uploads')

// 确保数据目录存在
if (!existsSync(DATA_DIR)) mkdirSync(DATA_DIR, { recursive: true })
if (!existsSync(UPLOADS_PATH)) mkdirSync(UPLOADS_PATH, { recursive: true })

const app = express()
app.use(cors())
app.use(express.json({ limit: '2mb' }))

// ── 文件上传配置 ──
const storage = multer.diskStorage({
  destination: (_req, _file, cb) => cb(null, UPLOADS_PATH),
  filename: (_req, file, cb) => {
    const ext = file.originalname.substring(file.originalname.lastIndexOf('.'))
    cb(null, Date.now() + '-' + Math.random().toString(36).slice(2, 8) + ext)
  },
})
const upload = multer({
  storage,
  limits: { fileSize: 10 * 1024 * 1024 }, // 10MB
  fileFilter: (_req, file, cb) => {
    if (file.mimetype.startsWith('image/')) cb(null, true)
    else cb(new Error('只允许上传图片文件'))
  },
})

// ── 数据读写 ──
function loadArticles(): any[] {
  if (!existsSync(ARTICLES_PATH)) return []
  return JSON.parse(readFileSync(ARTICLES_PATH, 'utf-8'))
}

function saveArticles(articles: any[]) {
  writeFileSync(ARTICLES_PATH, JSON.stringify(articles, null, 2), 'utf-8')
}

let nextId = loadArticles().length > 0 ? Math.max(...loadArticles().map(a => a.id)) + 1 : 1

// ── 认证中间件 ──
function auth(req: express.Request, res: express.Response, next: express.NextFunction) {
  const token = req.headers.authorization?.replace('Bearer ', '') || req.query.key as string
  if (token !== ADMIN_PASSWORD) {
    return res.status(401).json({ error: '未授权' })
  }
  next()
}

// ── 公开接口：获取已发布的文章 ──
app.get('/api/articles', (_req, res) => {
  const articles = loadArticles()
  res.json(articles.filter(a => a.published !== false))
})

// ── 认证接口 ──
app.post('/api/admin/auth', (req, res) => {
  const { password } = req.body
  if (password === ADMIN_PASSWORD) {
    res.json({ token: password })
  } else {
    res.status(401).json({ error: '密码错误' })
  }
})

// ── 管理接口（需认证）──
app.get('/api/admin/articles', auth, (_req, res) => {
  res.json(loadArticles())
})

app.post('/api/admin/articles', auth, (req, res) => {
  const articles = loadArticles()
  const article = {
    id: nextId++,
    title: req.body.title || '未命名',
    subtitle: req.body.subtitle || '',
    date: req.body.date || new Date().toLocaleDateString('zh-CN', { year: 'numeric', month: 'long', day: 'numeric' }),
    category: req.body.category || '随笔',
    excerpt: req.body.excerpt || '',
    content: req.body.content || '',
    published: req.body.published !== false,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  }
  articles.push(article)
  saveArticles(articles)
  res.json(article)
})

app.put('/api/admin/articles/:id', auth, (req, res) => {
  const articles = loadArticles()
  const id = parseInt(req.params.id)
  const idx = articles.findIndex(a => a.id === id)
  if (idx === -1) return res.status(404).json({ error: '文章不存在' })
  
  articles[idx] = {
    ...articles[idx],
    ...req.body,
    id,
    updatedAt: new Date().toISOString(),
  }
  saveArticles(articles)
  res.json(articles[idx])
})

app.delete('/api/admin/articles/:id', auth, (req, res) => {
  const articles = loadArticles()
  const id = parseInt(req.params.id)
  const filtered = articles.filter(a => a.id !== id)
  if (filtered.length === articles.length) return res.status(404).json({ error: '文章不存在' })
  saveArticles(filtered)
  res.json({ success: true })
})

// ── 图片上传 ──
app.post('/api/admin/upload', auth, (req, res) => {
  upload.single('image')(req, res, (err) => {
    if (err) return res.status(400).json({ error: err.message })
    if (!req.file) return res.status(400).json({ error: '请选择图片' })
    res.json({ url: '/uploads/' + req.file.filename })
  })
})

// ── 管理页面 ──
app.get('/admin', (_req, res) => {
  res.sendFile(ADMIN_PATH)
})

// ── 静态文件 ──
app.use(express.static(DIST_PATH))
app.use('/uploads', express.static(UPLOADS_PATH))

// SPA fallback
app.use((_req, res) => {
  res.sendFile(join(DIST_PATH, 'index.html'))
})

const PORT = parseInt(process.env.PORT || '3000')
app.listen(PORT, () => {
  console.log(`✓ 服务器运行在 http://localhost:${PORT}`)
  console.log(`  管理后台: http://localhost:${PORT}/admin`)
  console.log(`  密码: ${ADMIN_PASSWORD}`)
  console.log(`  数据目录: ${DATA_DIR}`)
})
