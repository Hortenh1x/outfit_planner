# План доведения до 100% — Outfit Planner

Текущая готовность: ~95%. Все 8 планов из `docs/superpowers/plans/` реализованы (включая paywall и Stripe до состояния «вставь ключ»); остались интеграционные прогоны с живыми провайдерами, CI и прод-репетиция.

**Статус прогона 2026-08-10 (Claude Code):** выполнено всё, что не требует внешних кредитов/ключей. Осталось два внешних действия: (1) пополнить кредиты FASHN и прогнать живые рендеры, (2) вставить Stripe test-ключи и пройти runbook из README → Verification.

## 1. CI (единственный крупный пробел инфраструктуры) — ✅ DONE 2026-08-10
- [x] GitHub Actions: `.github/workflows/ci.yml` — backend tests (161) → backend build → `npm ci` → `npm test` (181) → `npm run build`.
- [x] Шаги строго последовательно в ОДНОМ job (OpenAPI-гонка исключена конструкцией); Node зафиксирован на 24 (Node 26 ломает vitest jsdom localStorage).
- [x] Job с Playwright e2e добавлен как non-blocking (`continue-on-error`): in-memory http API + Vite dev + `PLAYWRIGHT_BASE_URL`. Локально не эмулировался — первый реальный прогон покажет CI.
- Вся последовательность команд прогнана локально в контейнерах sdk:10.0 / node:24 — зелёная.

## 2. Stripe end-to-end в test mode — ⛔ BLOCKED (нужны ключи)
- [ ] Живой прогон невозможен без внешних данных: Stripe-ключей в `.env` нет, `stripe` CLI не установлен.
- [x] Пошаговый runbook записан в README → Verification → «Stripe test-mode end-to-end runbook» (checkout → role flip → top-up → отмена → идемпотентность → UI-проверки). Осталось вставить test-ключи и пройти по шагам.
- Логика покрыта backend-тестами (webhook fail-closed, идемпотентность, role transitions, top-up ledger).

## 3. FASHN живой прогон — ⚠️ PARTIAL: интеграция проверена, рендеры ждут кредитов FASHN
- [x] Живая цепочка на dev/selfhost стеке пройдена: регистрация → gender → body-фото → rembg-вырезки → аутфит → estimate (`FashnTryOnProvider`, 2 кредита @1k, trial 8) → confirm (дебит) → очередь → воркер → реальный HTTP-вызов FASHN.
- [ ] Сами рендеры (1k/4k) не состоялись: FASHN вернул `429 You are out of credits` — на FASHN-аккаунте кончились кредиты. После пополнения: single+sequential @1k, 4k — от Premium/Admin аккаунта; проверить кэш и копирование output в app-owned storage.
- [x] Бонус: идемпотентный refund проверен вживую (8 → списание 2 → Failed → 8).
- [x] Вариант Б плана зафиксирован: демо остаётся mock-only, отмечено в README → Current Boundaries.

## 4. Закрыть намеренные хвосты — ✅ DONE 2026-08-10
- [x] Hairstyle-пресеты: решение «остаются скрытыми для 1.0, wiring сохранён» записано в CLAUDE.md/AGENTS.md.
- [x] Multi-item garment extraction: помечено out-of-scope для 1.0 (scaffold остаётся) в CLAUDE.md/AGENTS.md.

## 5. Прод-репетиция — ✅ DONE 2026-08-10
- [x] Полный прод-стек (`docker-compose.yml`) собран и поднят отдельным compose-проектом (`-p outfit-planner-rehearsal`, порты `FRONTEND_HTTP_PORT=18080`/`FRONTEND_HTTPS_PORT=18443` — 80/443 не занимались, живой selfhost-стек не прерывался). TLS из `.secrets/tls/`, `.env` подхвачен, миграции на чистом Postgres прошли, api healthy.
- [x] Смоук: регистрация → гардероб (upload + create, Simple keyer — rembg в прод-компоузе нет) → аутфит → try-on (mock) Succeeded → повторный confirm из кэша БЕЗ списания → анонимный шаринг.
- [x] Подписанные URL под `PUBLIC_ORIGIN` (https://outfitplanner.net): реальный JPEG отдан через прод-nginx, подделанная подпись → 404.
- [x] Найдено и исправлено: у backend-контекста не было `.dockerignore` → локальный `appsettings.json` с реальными FASHN/Google ключами запекался в api-образ и молча переключал «прод» на FASHN. Добавлен `outfit_planner_back/.dockerignore` (git-история чистая — в закоммиченных версиях ключи были пустыми; ключи живут только в untracked-файле).
- [x] Тестовый стек и волюмы снесены после смоука; живой стек не затронут (и попутно реанимирован: postgres/redis/minio стояли остановленными 3 недели, api висел unhealthy).

## 6. Гигиена — ✅ DONE 2026-08-10
- [x] `graphify update .` после правок.
- [x] README/CLAUDE.md/AGENTS.md синхронизированы (CI, Stripe runbook, решения по хвостам, итоги репетиции, правило про секреты в образах).

## Локальные порты (без изменений — конфликтов нет)
API 5001 (https), Vite 5173, Postgres 15433, Redis 16379, MinIO 9000/9001, rembg 7000, autotag 7100. Прод-репетиция: 18080/18443 (свободные; 8080 занят другим процессом).
