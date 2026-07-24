import './style.css'
import { seedPosts, categories, type BlogPost } from './data.ts'
import { getTodayQuote } from './quotes.ts'

let currentFilter = '全部'
let allPosts: BlogPost[] = seedPosts  // fallback to seed data
let currentView: 'list' | 'detail' = 'list'
const app = document.querySelector<HTMLDivElement>('#app')!

function buildLayout() {
  const year = new Date().getFullYear()
  app.innerHTML = `
    <header class="site-header">
      <a class="logo" href="#" id="home-link">
        <span>观澜</span>
        <span class="dot"></span>
      </a>
      <nav class="nav">
        <a href="#" class="active">文章</a>
      </nav>
    </header>
    <main class="main-content">
      <section class="hero-section" id="hero-section">
        <h1 class="motto">观水有术，必观其澜</h1>
        <p class="sub-motto">在喧嚣的时代，守护沉静思考的权利</p>
      </section>
      <section class="calendar-section" id="calendar-section">
        <div class="calendar-inner">
          <div class="today-display" id="today-display">
            <div class="today-date">
              <span class="today-month" id="today-month"></span>
              <span class="today-day" id="today-day"></span>
            </div>
            <div class="daily-quote" id="daily-quote">
              <p class="quote-mark">“</p>
              <p class="quote-text"></p>
              <p class="quote-source"></p>
            </div>
          </div>
        </div>
      </section>
      <div class="hero-divider"></div>
      <p class="section-subtitle">记 录 思 想 的 痕 迹</p>
      <div class="filter-bar" id="filter-bar"></div>
      <div id="content-area">
        <div class="article-grid" id="article-grid"></div>
        <div class="article-detail" id="article-detail"></div>
      </div>
    </main>
    <footer class="site-footer">
      <div class="footer-links">
        <a href="#">GitHub</a>
        <a href="#">豆瓣</a>
        <a href="#">微博</a>
        <a href="#">RSS</a>
      </div>
      <div class="copyright">© ${year} 观澜 · 保留所有权利</div>
    </footer>
  `
}

function buildFilters() {
  const bar = document.getElementById('filter-bar')!
  bar.innerHTML = `
    <div class="filter-categories">
      ${categories.map(c =>
        `<button class="filter-btn${c === currentFilter ? ' active' : ''}" data-category="${c}">${c}</button>`
      ).join('')}
    </div>
  `
  bar.addEventListener('click', (e) => {
    const btn = (e.target as HTMLElement).closest('.filter-btn') as HTMLElement | null
    if (!btn) return
    const cat = btn.dataset.category!
    currentFilter = cat
    bar.querySelectorAll('.filter-btn').forEach(b => b.classList.remove('active'))
    btn.classList.add('active')

    renderArticles()
  })

}


function renderTodayDisplay() {
  const now = new Date()
  const month = now.getMonth() + 1
  const day = now.getDate()
  const quote = getTodayQuote()

  const monthEl = document.getElementById('today-month')
  const dayEl = document.getElementById('today-day')
  const qText = document.querySelector('.quote-text')
  const qSrc = document.querySelector('.quote-source')

  if (monthEl) monthEl.textContent = `${month}月`
  if (dayEl) dayEl.textContent = `${day}`

  if (quote && qText && qSrc) {
    qText.textContent = quote.quote
    qSrc.textContent = quote.source
  }
}

async function initArticles() {
  try {
    const res = await fetch('/api/articles')
    if (res.ok) {
      const data = await res.json()
      allPosts = data.map((a: any) => ({
        id: a.id,
        title: a.title,
        subtitle: a.subtitle || '',
        date: a.date,
        category: a.category,
        excerpt: a.excerpt || '',
        content: a.content || '',
      }))
    }
  } catch { /* fallback to seedPosts */ }
  renderArticles()
}

function renderArticles() {
  const grid = document.getElementById('article-grid')!
  const detail = document.getElementById('article-detail')!
  const hero = document.getElementById('hero-section')!
  const filterBar = document.getElementById('filter-bar')!
  grid.style.display = ''
  detail.classList.remove('visible')
  hero.style.display = ''
  filterBar.style.display = ''
  document.title = '观澜 · 个人博客'

  const filtered = currentFilter === '全部' ? allPosts : allPosts.filter(p => p.category === currentFilter)

  if (filtered.length === 0) {
    grid.innerHTML = '<div class="empty-state"><p>该分类下暂无文章</p></div>'
    return
  }

  grid.innerHTML = filtered.map(post => `
    <article class="article-card" data-id="${post.id}">
      <div class="card-meta">
        <span class="tag">${post.category}</span>
        <span>·</span>
        <time>${post.date}</time>
      </div>
      <h2 class="card-title">${post.title}</h2>
      <p class="card-subtitle">${post.subtitle}</p>
      <p class="card-excerpt">${post.excerpt}</p>
      <span class="card-arrow">阅读全文 →</span>
    </article>
  `).join('')

  grid.querySelectorAll('.article-card').forEach(card => {
    card.addEventListener('click', () => {
      const id = Number((card as HTMLElement).dataset.id)
      const post = allPosts.find(p => p.id === id)
      if (post) showDetail(post)
    })
  })
}

function renderImageTag(text: string): string {
  const m = text.match(/^!\[(.*?)\]\((.*?)(?:\s+"(.*?)")?\)$/)
  if (!m) return ''
  const alt = m[1], url = m[2]
  const opts = (m[3] || 'l c').split(' ')
  let size = opts[0] || 'l', align = opts[1] || 'c'
  let cls = 'article-image article-img-' + size + ' article-img-' + align
  return '<figure class="article-figure"><img class="' + cls + '" src="' + url + '" alt="' + alt + '" loading="lazy" /><figcaption class="article-caption">' + alt + '</figcaption></figure>'
}

function renderInline(text: string): string {
  text = text.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
  text = text.replace(/\*(.+?)\*/g, '<em>$1</em>')
  text = text.replace(/!\[(.*?)\]\((.*?)\)/g, '<img class="article-image-inline" src="$2" alt="$1" loading="lazy" />')
  text = text.replace(/\[(.+?)\]\((.*?)\)/g, '<a href="$2" target="_blank" rel="noopener">$1</a>')
  return text
}

function renderParagraph(text: string): string {
  const imgMatch = text.match(/^!\[(.*?)\]\((.*?)(?:\s+"(.*?)")?\)$/)
  if (imgMatch) return renderImageTag(text)
  if (text.startsWith('## ')) return '<h2>' + renderInline(text.slice(3)) + '</h2>'
  if (text.startsWith('### ')) return '<h3>' + renderInline(text.slice(4)) + '</h3>'
  if (text.startsWith('> ')) return '<blockquote>' + renderInline(text.slice(2)) + '</blockquote>'
  if (text.startsWith('- ')) return '<li>' + renderInline(text.slice(2)) + '</li>'
  if (text.startsWith('* ')) return '<li>' + renderInline(text.slice(2)) + '</li>'
  return '<p>' + renderInline(text) + '</p>'
}

function renderAllParagraphs(text: string): string {
  const ps = text.split('\n\n').map(p => p.trim()).filter(Boolean)
  let html = '', i = 0
  while (i < ps.length) {
    // 连续图片 → 画廊
    const imgStart = i
    while (i < ps.length && ps[i].match(/^!\[(.*?)\]\((.*?)\)/)) i++
    if (i - imgStart >= 2) {
      html += '<div class="article-gallery">'
      for (let j = imgStart; j < i; j++) html += renderImageTag(ps[j])
      html += '</div>'
      continue
    }
    i = imgStart
    // 连续列表项
    const listStart = i
    while (i < ps.length && (ps[i].startsWith('- ') || ps[i].startsWith('* '))) i++
    if (i - listStart >= 1) {
      html += '<ul class="article-list">'
      for (let j = listStart; j < i; j++) html += renderParagraph(ps[j])
      html += '</ul>'
      continue
    }
    i = listStart
    html += renderParagraph(ps[i])
    i++
  }
  return html
}

let currentArticleId = 0

async function loadComments(articleId: number) {
  currentArticleId = articleId
  try {
    const res = await fetch('/api/comments/' + articleId)
    if (!res.ok) return
    const comments = await res.json()
    const list = document.getElementById('comment-list')!
    const count = document.getElementById('comment-count')!
    count.textContent = String(comments.length)
    if (comments.length === 0) {
      list.innerHTML = '<p class="comment-empty">\u6682\u65e0\u8bc4\u8bba</p>'
      return
    }
    list.innerHTML = comments.map((c: any) =>
      '<div class="comment-item">' +
        '<div class="comment-meta">' +
          '<span class="comment-name">' + escHtml(c.name) + '</span>' +
          '<span class="comment-time">' + fmtTime(c.created_at) + '</span>' +
        '</div>' +
        '<div class="comment-text">' + escHtml(c.content) + '</div>' +
      '</div>'
    ).join('')
  } catch {}
}

function escHtml(t: string): string {
  const d = document.createElement('div')
  d.textContent = t
  return d.innerHTML
}

function fmtTime(t: string): string {
  try { const d = new Date(t); return d.getFullYear() + '\u5e74' + (d.getMonth() + 1) + '\u6708' + d.getDate() + '\u65e5' } catch { return t }
}

(window as any).submitComment = async function() {
  const name = (document.getElementById('comment-name') as HTMLInputElement).value.trim()
  const email = (document.getElementById('comment-email') as HTMLInputElement).value.trim()
  const content = (document.getElementById('comment-content') as HTMLTextAreaElement).value.trim()
  const status = document.getElementById('comment-status')!
  const btn = document.getElementById('comment-submit') as HTMLButtonElement
  if (!name) { status.textContent = '\u8bf7\u586b\u5199\u6635\u79f0'; return }
  if (!content) { status.textContent = '\u8bf7\u586b\u5199\u8bc4\u8bba\u5185\u5bb9'; return }
  btn.disabled = true; btn.textContent = '\u63d0\u4ea4\u4e2d...'
  try {
    const res = await fetch('/api/comments', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ article_id: currentArticleId, name, email, content }) })
    const data = await res.json()
    if (data.success) {
      status.textContent = '\u8bc4\u8bba\u5df2\u63d0\u4ea4\uff0c\u5ba1\u6838\u901a\u8fc7\u540e\u5c06\u663e\u793a\u5728\u8fd9\u91cc'
      status.className = 'comment-status success'
      ;(document.getElementById('comment-name') as HTMLInputElement).value = ''
      ;(document.getElementById('comment-content') as HTMLTextAreaElement).value = ''
      ;(document.getElementById('comment-char-count') as HTMLSpanElement).textContent = '0/2000'
    } else { status.textContent = data.error || '\u63d0\u4ea4\u5931\u8d25'; status.className = 'comment-status error' }
  } catch { status.textContent = '\u7f51\u7edc\u9519\u8bef'; status.className = 'comment-status error' }
  btn.disabled = false; btn.textContent = '\u53d1\u8868\u8bc4\u8bba'
}

document.addEventListener('input', (e) => {
  const target = e.target as HTMLElement
  if (target.id === 'comment-content') {
    const len = (target as HTMLTextAreaElement).value.length
    const el = document.getElementById('comment-char-count')
    if (el) el.textContent = len + '/2000'
  }
})

function showDetail(post: BlogPost) {
  currentView = 'detail'
  const grid = document.getElementById('article-grid')!
  const detail = document.getElementById('article-detail')!
  const hero = document.getElementById('hero-section')!
  const filterBar = document.getElementById('filter-bar')!
  const calSection = document.getElementById('calendar-section')!
  grid.style.display = 'none'
  hero.style.display = 'none'
  filterBar.style.display = 'none'
  calSection.style.display = 'none'
  detail.classList.add('visible')
  window.scrollTo({ top: 80, behavior: 'smooth' })
  document.title = `${post.title} · 观澜`

  detail.innerHTML = `
    <button class="detail-back" id="back-btn">
      <span class="back-arrow">←</span>
      <span>返回文章列表</span>
    </button>
    <header class="detail-header">
      <div class="detail-meta">
        <span class="tag">${post.category}</span>
        <span>·</span>
        <time>${post.date}</time>
      </div>
      <h1 class="detail-title">${post.title}</h1>
      <p class="detail-subtitle">${post.subtitle}</p>
    </header>
    <div class="detail-body">
      ${renderAllParagraphs(post.content)}
    </div>
    <section class="comment-section">
      <h3 class="comment-heading">评论 <span class="comment-count" id="comment-count">0</span></h3>
      <div class="comment-list" id="comment-list"></div>
      <div class="comment-form-wrap">
        <p class="comment-form-title">发表评论</p>
        <div class="comment-form-row">
          <input type="text" id="comment-name" placeholder="昵称" maxlength="30" required />
          <input type="email" id="comment-email" placeholder="邮箱（选填）" />
        </div>
        <textarea id="comment-content" placeholder="写下你的评论..." maxlength="2000" required></textarea>
        <div class="comment-form-actions">
          <span class="comment-char-count" id="comment-char-count">0/2000</span>
          <button class="comment-submit" id="comment-submit">发表评论</button>
        </div>
        <p class="comment-status" id="comment-status"></p>
      </div>
    </section>
  `
  document.getElementById('back-btn')!.addEventListener('click', showList)
  document.addEventListener('keydown', onKeyDown)
  const submitBtn = document.getElementById("comment-submit")
  if (submitBtn) submitBtn.onclick = (window as any).submitComment
  loadComments(post.id)
}

function showList() {
  currentView = 'list'
  const grid = document.getElementById('article-grid')!
  const detail = document.getElementById('article-detail')!
  const hero = document.getElementById('hero-section')!
  const filterBar = document.getElementById('filter-bar')!
  const calSection = document.getElementById('calendar-section')!
  detail.classList.remove('visible')
  hero.style.display = ''
  filterBar.style.display = ''
  calSection.style.display = ''
  grid.style.display = ''
  document.title = '观澜 · 个人博客'
  window.scrollTo({ top: 80, behavior: 'smooth' })
  document.removeEventListener('keydown', onKeyDown)
  renderArticles()
}

function onKeyDown(e: KeyboardEvent) {
  if (e.key === 'Escape' && currentView === 'detail') showList()
}

buildLayout()
buildFilters()
initArticles()
renderTodayDisplay()

document.getElementById('home-link')!.addEventListener('click', (e) => {
  e.preventDefault()
  if (currentView === 'detail') showList()
  window.scrollTo({ top: 0, behavior: 'smooth' })
})


