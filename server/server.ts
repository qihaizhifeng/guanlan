import express from 'express'
import cors from 'cors'
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'fs'
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
const ARTICLES_PATH = join(DATA_DIR, 'articles.json')
const COMMENTS_PATH = join(DATA_DIR, 'comments.json')

if (!existsSync(UPLOADS_PATH)) mkdirSync(UPLOADS_PATH, { recursive: true })

// ── 评论频率限制 ──
const commentRate = new Map<string, number>()
const RATE_LIMIT_MS = 30000 // 30秒

function checkRate(ip: string): boolean {
  const last = commentRate.get(ip) || 0
  if (Date.now() - last < RATE_LIMIT_MS) return false
  commentRate.set(ip, Date.now())
  return true
}

const app = express()
app.use(cors())
app.use(express.json({ limit: '2mb' }))

// ── 数据库 ──
let db: pg.Pool | null = null

if (DATABASE_URL) {
  db = new pg.Pool({ connectionString: DATABASE_URL })
  db.query(`CREATE TABLE IF NOT EXISTS articles (id SERIAL PRIMARY KEY,title TEXT DEFAULT '',subtitle TEXT DEFAULT '',date TEXT DEFAULT '',category TEXT DEFAULT '随笔',excerpt TEXT DEFAULT '',content TEXT DEFAULT '',published BOOLEAN DEFAULT true,created_at TIMESTAMPTZ DEFAULT NOW(),updated_at TIMESTAMPTZ DEFAULT NOW())`).catch(e => { console.error('DB init failed:', e.message); db = null })
  db.query(`CREATE TABLE IF NOT EXISTS comments (id SERIAL PRIMARY KEY,article_id INTEGER NOT NULL,name TEXT NOT NULL DEFAULT '',email TEXT DEFAULT '',content TEXT NOT NULL DEFAULT '',status TEXT DEFAULT 'pending',created_at TIMESTAMPTZ DEFAULT NOW())`).catch(e => { console.error('Comments table failed:', e.message) })
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
    // JSON file fallback
    try {
      const data = JSON.parse(readFileSync(ARTICLES_PATH, 'utf-8'))
      res.json(data.filter((a: any) => a.published !== false))
    } catch { res.json([]) }
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
    try {
      const data = JSON.parse(readFileSync(ARTICLES_PATH, 'utf-8'))
      res.json(data)
    } catch { res.json([]) }
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
  if (!db) {
    try {
      const id = parseInt(req.params.id)
      const data = JSON.parse(readFileSync(ARTICLES_PATH, 'utf-8'))
      const idx = data.findIndex((a: any) => a.id === id)
      if (idx === -1) return res.status(404).json({ error: '\u6587\u7ae0\u4e0d\u5b58\u5728' })
      data[idx] = { ...data[idx], ...req.body, id, updated_at: new Date().toISOString() }
      writeFileSync(ARTICLES_PATH, JSON.stringify(data, null, 2), 'utf-8')
      return res.json(data[idx])
    } catch (e) {
      return res.status(500).json({ error: 'Failed to update' })
    }
  }
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
  if (!db) {
    try {
      const id = parseInt(req.params.id)
      const data = JSON.parse(readFileSync(ARTICLES_PATH, 'utf-8'))
      const filtered = data.filter((a: any) => a.id !== id)
      if (filtered.length === data.length) return res.status(404).json({ error: '\u6587\u7ae0\u4e0d\u5b58\u5728' })
      writeFileSync(ARTICLES_PATH, JSON.stringify(filtered, null, 2), 'utf-8')
      return res.json({ success: true })
    } catch (e) {
      return res.status(500).json({ error: 'Failed to delete' })
    }
  }
  const id = parseInt(req.params.id)
  const result = await db.query('DELETE FROM articles WHERE id=$1 RETURNING id', [id])
  if (result.rows.length === 0) return res.status(404).json({ error: '\u6587\u7ae0\u4e0d\u5b58\u5728' })
  res.json({ success: true })
})

// ── 评论接口（公开）──
app.get('/api/comments/:articleId', async (req, res) => {
  const articleId = parseInt(req.params.articleId)
  if (!articleId) return res.status(400).json({ error: 'Invalid article ID' })
  if (db) {
    const result = await db.query('SELECT id,name,content,created_at FROM comments WHERE article_id=$1 AND status=$2 ORDER BY created_at ASC', [articleId, 'approved'])
    res.json(result.rows)
  } else {
    try {
      const data = JSON.parse(readFileSync(COMMENTS_PATH, 'utf-8'))
      res.json(data.filter((c: any) => c.article_id === articleId && c.status === 'approved'))
    } catch { res.json([]) }
  }
})

app.post('/api/comments', async (req, res) => {
  const ip = req.ip || req.socket.remoteAddress || 'unknown'
  if (!checkRate(ip)) return res.status(429).json({ error: '评论过快，请稍后再试' })
  const { article_id, name, email, content } = req.body
  if (!article_id || !name || !content) return res.status(400).json({ error: '请填写必填字段' })
  if (name.length > 30) return res.status(400).json({ error: '昵称不能超过30字' })
  if (content.length > 2000) return res.status(400).json({ error: '评论不能超过2000字' })

  if (db) {
    const result = await db.query('INSERT INTO comments (article_id,name,email,content) VALUES ($1,$2,$3,$4) RETURNING id,name,created_at', [article_id, name.trim(), email || '', content.trim()])
    res.json({ success: true, comment: result.rows[0] })
  } else {
    try {
      const data = JSON.parse(readFileSync(COMMENTS_PATH, 'utf-8'))
      const c = { id: data.length + 1, article_id, name: name.trim(), email: email || '', content: content.trim(), status: 'pending', created_at: new Date().toISOString() }
      data.push(c)
      writeFileSync(COMMENTS_PATH, JSON.stringify(data, null, 2), 'utf-8')
      res.json({ success: true, comment: { id: c.id, name: c.name, created_at: c.created_at } })
    } catch { res.status(500).json({ error: 'Failed to save' }) }
  }
})

// ── 评论管理接口（需认证）──
app.get('/api/admin/comments', auth, async (req, res) => {
  const status = req.query.status as string
  if (db) {
    let sql = 'SELECT c.*, a.title as article_title FROM comments c LEFT JOIN articles a ON c.article_id=a.id'
    if (status) sql += ' WHERE c.status=$1'
    sql += ' ORDER BY c.created_at DESC'
    const params = status ? [status] : []
    const result = await db.query(sql, params)
    res.json(result.rows)
  } else {
    try {
      const data = JSON.parse(readFileSync(COMMENTS_PATH, 'utf-8'))
      const articles = JSON.parse(readFileSync(ARTICLES_PATH, 'utf-8'))
      const result = data.map((c: any) => ({ ...c, article_title: (articles.find((a: any) => a.id === c.article_id) || {}).title || '' }))
      res.json(status ? result.filter((c: any) => c.status === status) : result)
    } catch { res.json([]) }
  }
})

app.put('/api/admin/comments/:id', auth, async (req, res) => {
  if (!db) return res.status(500).json({ error: 'Only available with DB' })
  const id = parseInt(req.params.id)
  const { status } = req.body
  if (!['pending', 'approved', 'rejected'].includes(status)) return res.status(400).json({ error: 'Invalid status' })
  const result = await db.query('UPDATE comments SET status=$1 WHERE id=$2 RETURNING *', [status, id])
  if (result.rows.length === 0) return res.status(404).json({ error: 'Comment not found' })
  res.json(result.rows[0])
})

app.delete('/api/admin/comments/:id', auth, async (req, res) => {
  const id = parseInt(req.params.id)
  if (db) {
    const result = await db.query('DELETE FROM comments WHERE id=$1 RETURNING id', [id])
    if (result.rows.length === 0) return res.status(404).json({ error: 'Comment not found' })
  } else {
    try {
      const data = JSON.parse(readFileSync(COMMENTS_PATH, 'utf-8'))
      const filtered = data.filter((c: any) => c.id !== id)
      if (filtered.length === data.length) return res.status(404).json({ error: 'Comment not found' })
      writeFileSync(COMMENTS_PATH, JSON.stringify(filtered, null, 2), 'utf-8')
    } catch { return res.status(500).json({ error: 'Failed to delete' }) }
  }
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
  console.log('Running at http://localhost:' + PORT)
  console.log('Admin: http://localhost:' + PORT + '/admin')
  console.log('Password: ' + ADMIN_PASSWORD)
  if (db) console.log('  Database: PostgreSQL')
  else console.log('  Database: not connected')
})