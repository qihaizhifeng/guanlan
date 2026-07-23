import express from 'express'
import cors from 'cors'
import { existsSync, mkdirSync } from 'fs'
import { join, dirname } from 'path'
import { fileURLToPath } from 'url'
import multer from 'multer'
import pg from 'pg'

const __dirname = dirname(fileURLToPath(import.meta.url))
const DIST_PATH = join(__dirname, '..', 'dist')
const ADMIN_PATH = join(__dirname, '..', 'admin', 'index.html')
const DATA_DIR = process.env.DATA_DIR || __dirname
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD || 'guanlan2024'
const DATABASE_URL = process.env.DATABASE_URL || ''

const UPLOADS_PATH = join(DATA_DIR, 'uploads')

if (!existsSync(UPLOADS_PATH)) mkdirSync(UPLOADS_PATH, { recursive: true })

const app = express()
app.use(cors())
app.use(express.json({ limit: '2mb' }))

// ── 数据库 ──
let db: pg.Pool | null = null

if (DATABASE_URL) {
  db = new pg.Pool({ connectionString: DATABASE_URL })
  db.query(`
    CREATE TABLE IF NOT EXISTS articles (
      id SERIAL PRIMARY KEY,
      title TEXT NOT NULL DEFAULT '',
      subtitle TEXT NOT NULL DEFAULT '',
      date TEXT NOT NULL DEFAULT '',
      category TEXT NOT NULL DEFAULT '随笔',
      excerpt TEXT NOT NULL DEFAULT '',
      content TEXT NOT NULL DEFAULT '',
      published BOOLEAN NOT NULL DEFAULT true,
      created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
      updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
    )').catch(e => { console.error('数据库初始化失败:', e.message); db = null })
  console.log('✓ 使用 PostgreSQL')
} else {
  console.log('✓ 无数据库，仅提供静态文件服务')
}

// ── 认证中间件 ──
function auth(req: express.Request, res: express.Response, next: express.NextFunction) {
  const token = req.headers.authorization?.replace('Bearer ', '') || req.query.key as string
  if (token !== ADMIN_PASSWORD) {
    return res.status(401).json({ error: '未授权' })
  }
  next()
}

// ── 公开接口 ──
app.get('/api/articles', async (_req, res) => {
  if (db) {
    const result = await db.query('SELECT * FROM articles WHERE published = true ORDER BY id DESC')
    res.json(result.rows)
  } else {
    res.json([])
  }
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

// ── 管理接口 ──
app.get('/api/admin/articles', auth, async (_req, res) => {
  if (db) {
    const result = await db.query('SELECT * FROM articles ORDER BY id DESC')
    res.json(result.rows)
  } else {
    res.json([])
  }
})

app.post('/api/admin/articles', auth, async (req, res) => {
  if (!db) return res.status(500).json({ error: '数据库未连接' })
  const result = await db.query(
    'INSERT INTO articles (title, subtitle, date, category, excerpt, content, published) VALUES ($1,$2,$3,$4,$5,$6,$7) RETURNING *',
    [
      req.body.title || '未命名',
      req.body.subtitle || '',
      req.body.date || new Date().toLocaleDateString('zh-CN', { year: 'numeric', month: 'long', day: 'numeric' }),
      req.body.category || '随笔',
      req.body.excerpt || '',
      req.body.content || '',
      req.body.published !== false,
    ]
  )
  res.json(result.rows[0])
})

app.put('/api/admin/articles/:id', auth, async (req, res) => {
  if (!db) return res.status(500).json({ error: '\u6570\u636e\u5e93\u672a\u8fde\u63a5' })
  const id = parseInt(req.params.id)
  const result = await db.query(
    'UPDATE articles SET title=$1,subtitle=$2,date=$3,category=$4,excerpt=$5,content=$6,published=$7,updated_at=NOW() WHERE id=$8 RETURNING *',
    [
      req.body.title || '',
      req.body.subtitle || '',
      req.body.date || '',
      req.body.category || '',
      req.body.excerpt || '',
      req.body.content || '',
      req.body.published !== false,
      id,
    ]
  )
  if (result.rows.length === 0) return res.status(404).json({ error: '\u6587\u7ae0\u4e0d\u5b58\u5728' })
  res.json(result.rows[0])
})

app.delete('/api/admin/articles/:id', auth, async (req, res) => {
  if (!db) return res.status(500).json({ error: '\u6570\u636e\u5e93\u672a\u8fde\u63a5' })
  const id = parseInt(req.params.id)
  const result = await db.query('DELETE FROM articles WHERE id=$1 RETURNING id', [id])
  if (result.rows.length === 0) return res.status(404).json({ error: '\u6587\u7ae0\u4e0d\u5b58\u5728' })
  res.json({ success: true })
})

// ── 图片上传 ──
const storage = multer.diskStorage({
  destination: (_req, _file, cb) => cb(null, UPLOADS_PATH),
  filename: (_req, file, cb) => {
    const ext = file.originalname.substring(file.originalname.lastIndexOf('.'))
    cb(null, Date.now() + '-' + Math.random().toString(36).slice(2, 8) + ext)
  },
})
const upload = multer({
  storage,
  limits: { fileSize: 10 * 1024 * 1024 },
  fileFilter: (_req, file, cb) => {
    if (file.mimetype.startsWith('image/')) cb(null, true)
    else cb(new Error('\u53ea\u5141\u8bb8\u4e0a\u4f20\u56fe\u7247\u6587\u4ef6'))
  },
})

app.post('/api/admin/upload', auth, (req, res) => {
  upload.single('image')(req, res, (err) => {
    if (err) return res.status(400).json({ error: err.message })
    if (!req.file) return res.status(400).json({ error: '\u8bf7\u9009\u62e9\u56fe\u7247' })
    res.json({ url: '/uploads/' + req.file.filename })
  })
})

// ── 管理页面 ──
app.get('/admin', (_req, res) => { res.sendFile(ADMIN_PATH) })

// ── 静态文件 ──
app.use(express.static(DIST_PATH))
app.use('/uploads', express.static(UPLOADS_PATH))

// SPA fallback
app.use((_req, res) => {
  res.sendFile(join(DIST_PATH, 'index.html'))
})

const PORT = parseInt(process.env.PORT || '3000')
app.listen(PORT, () => {
  console.log(`\u2713 \u670d\u52a1\u5668\u8fd0\u884c\u5728 http://localhost:\${PORT}`)
  console.log(`  \u7ba1\u7406\u540e\u53f0: http://localhost:\${PORT}/admin`)
  console.log(`  \u5bc6\u7801: \${ADMIN_PASSWORD}`)
  if (db) console.log('  \u6570\u636e\u5e93: PostgreSQL')
  else console.log('  \u6570\u636e\u5e93: \u672a\u8fde\u63a5')
})