#!/usr/bin/env node

import { readFileSync, writeFileSync } from 'node:fs'
import { createInterface } from 'node:readline'
import { stdin, stdout, argv } from 'node:process'

const DATA_FILE = new URL('../src/data.ts', import.meta.url).pathname

function ask(query) {
  return new Promise(resolve => {
    const rl = createInterface({ input: stdin, output: stdout })
    rl.question(query, answer => { rl.close(); resolve(answer.trim()) })
  })
}

function today() {
  const d = new Date()
  return `${d.getFullYear()}年${d.getMonth() + 1}月${d.getDate()}日`
}

function esc(s) {
  return s.replace(/\\/g, '\\\\').replace(/`/g, '\\`').replace(/\${/g, '\\${')
}

async function main() {
  console.log('\n  观澜 · 写新文章\n')
  const title = await ask('  标题：')
  if (!title) { console.log('  标题不能为空\n'); process.exit(1) }
  const subtitle = await ask('  副标题：')
  const category = await ask('  分类（随笔/书评/思考/摄影/记忆）：') || '随笔'

  console.log('\n  正文（输入或粘贴，空行分段落，Ctrl+D 结束）：\n')
  const lines = []
  const rl = createInterface({ input: stdin })
  for await (const line of rl) lines.push(line)
  const content = lines.join('\n').trim()
  if (!content) { console.log('  正文不能为空\n'); process.exit(1) }

  const firstPara = content.split('\n\n')[0]?.trim() || ''
  const excerpt = firstPara.length > 120 ? firstPara.slice(0, 120) + '……' : firstPara

  const src = readFileSync(DATA_FILE, 'utf-8')

  let maxId = 0
  for (const m of src.matchAll(/^\s+id:\s*(\d+)/gm)) {
    maxId = Math.max(maxId, parseInt(m[1], 10))
  }
  const newId = maxId + 1

  const entry = [
    '  {',
    `    id: ${newId},`,
    `    title: '${title.replace(/'/g, "\\'")}',`,
    `    subtitle: '${subtitle.replace(/'/g, "\\'")}',`,
    `    date: '${today()}',`,
    `    category: '${category}',`,
    `    excerpt: '${excerpt.replace(/'/g, "\\'")}',`,
    `    content: \`${esc(content)}\``,
    '  },'
  ].join('\n')

  const insertPos = src.indexOf('[') + 1
  const result = src.slice(0, insertPos) + '\n' + entry + src.slice(insertPos)
  writeFileSync(DATA_FILE, result, 'utf-8')
  console.log(`\n  ✓ 文章《${title}》已添加到 data.ts\n`)
  console.log('  运行 npm run dev 预览，npm run build 构建发布\n')
}

main().catch(err => { console.error(err); process.exit(1) })
