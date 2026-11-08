/* Local interaction mockup. No Unity connection, network requests or game-data writes. */
(() => {
  'use strict';
  const root = document.getElementById('battle-app');
  const $ = id => document.getElementById(id);
  const parts = [
    { id: 'Torso', name: '몸통', hp: 3500, multiplier: 1 },
    { id: 'Head', name: '머리', hp: 1000, multiplier: .8 },
    { id: 'Cockpit', name: '콕피트', hp: 600, multiplier: .45 },
    { id: 'RightArm', name: '오른팔', hp: 1400, multiplier: .9 },
    { id: 'LeftArm', name: '왼팔', hp: 1400, multiplier: .9 },
    { id: 'RightLeg', name: '오른다리', hp: 1600, multiplier: .9 },
    { id: 'LeftLeg', name: '왼다리', hp: 1600, multiplier: .9 }
  ];
  const weapons = [
    { id: 'rifle', name: '빔 라이플', slot: 'R', category: '빔 · 원거리', range: 7, ap: 3, min: 700, max: 1000, baseHit: 78, capacity: 6, reload: true },
    { id: 'axe', name: '전투 도끼', slot: 'L', category: '날붙이 · 근접', range: 2, ap: 3, min: 900, max: 1200, baseHit: 95, capacity: null, reload: false },
    { id: 'backpack', name: '백팩 포', slot: 'BP', category: '실탄 · 원거리', range: 9, ap: 3, min: 850, max: 1100, baseHit: 72, capacity: 4, reload: false }
  ];
  let state, toastTimer, phaseTimer;
  const makeHP = () => Object.fromEntries(parts.map(p => [p.id, p.hp]));
  function initialState() {
    return { turn: 3, phase: 'player', selected: 0, mode: 'attack', weapon: 'rifle', target: 0, part: 'Torso', logs: [], zoom: 1,
      allies: [
        { name: '아군 01', pilot: '한서진', code: 'VANGUARD', ap: 5, x: 33, y: 77, hp: makeHP(), ammo: { rifle: 4, backpack: 2 }, spares: 2 },
        { name: '아군 02', pilot: '윤태오', code: 'ANCHOR', ap: 5, x: 16, y: 48, hp: makeHP(), ammo: { rifle: 6, backpack: 4 }, spares: 2 },
        { name: '아군 03', pilot: '강리나', code: 'PATHFINDER', ap: 5, x: 31, y: 25, hp: makeHP(), ammo: { rifle: 3, backpack: 1 }, spares: 1 }
      ],
      enemies: [
        { name: 'ENEMY 02', x: 68, y: 43, hp: makeHP(), cover: '반엄폐', modifier: 1 },
        { name: 'ENEMY 01', x: 51, y: 16, hp: makeHP(), cover: '완전엄폐', modifier: .75 },
        { name: 'ENEMY 03', x: 87, y: 67, hp: makeHP(), cover: '엄폐 없음', modifier: 1.15 }
      ] };
  }
  const current = () => state.selected === null ? null : state.allies[state.selected];
  const weapon = () => weapons.find(w => w.id === state.weapon);
  const target = () => state.target === null ? null : state.enemies[state.target];
  function status(unit) {
    if (unit.ejected) return '탈출 완료';
    if (unit.hp.Torso <= 0 || unit.hp.Cockpit <= 0 || parts.filter(p => !['Torso', 'Cockpit'].includes(p.id) && unit.hp[p.id] === 0).length >= 4) return '전투불능';
    if (parts.filter(p => !['Torso', 'Cockpit'].includes(p.id) && unit.hp[p.id] === 0).length >= 3) return 'CRITICAL';
    return '정상';
  }
  const canAct = u => u && !u.ejected && status(u) === '정상' && !u.guard && state.phase === 'player';
  const distance = (a, b) => Math.max(1, Math.ceil((Math.abs(a.x - b.x) + Math.abs(a.y - b.y)) / 12));
  const pips = ap => `<div class="pips" aria-label="AP ${ap} / 5">${Array.from({ length: 5 }, (_, i) => `<i class="${i < ap ? 'filled' : ''}"></i>`).join('')}</div>`;
  function mechSVG(hp = makeHP(), active = '', extra = '') {
    const cls = id => 'part' + (hp[id] <= 0 ? ' destroyed' : hp[id] < parts.find(p => p.id === id).hp ? ' damaged' : '') + (id === active ? ' active' : '');
    const pick = id => active ? `data-part="${id}"` : '';
    return `<svg class="mech-svg ${extra}" viewBox="0 0 160 205" role="img" aria-label="메카 부위 상태"><ellipse cx="80" cy="197" rx="57" ry="5" fill="#0a140c" opacity=".4"/>
      <path ${pick('RightLeg')} class="${cls('RightLeg')}" d="M51 119 L76 119 L74 161 L69 188 L39 190 L43 177 L47 156 Z"/>
      <path ${pick('LeftLeg')} class="${cls('LeftLeg')}" d="M85 119 L110 119 L114 156 L118 177 L122 190 L91 188 L87 161 Z"/>
      <path class="shade" d="M48 153 L71 155 L69 167 L45 163 Z M90 155 L113 153 L115 163 L91 167 Z"/>
      <path class="joint" d="M48 108 L111 108 L114 127 L84 132 L78 132 L46 127 Z"/>
      <path ${pick('RightArm')} class="${cls('RightArm')}" d="M17 46 L47 43 L49 64 L42 89 L38 113 L33 132 L15 129 L11 112 L15 86 L12 65 Z"/>
      <path ${pick('LeftArm')} class="${cls('LeftArm')}" d="M113 43 L143 46 L148 65 L144 86 L149 112 L145 129 L127 132 L122 113 L118 89 L111 64 Z"/>
      <path class="shade" d="M15 80 L43 83 L41 96 L13 93 Z M118 83 L145 80 L147 93 L120 96 Z"/>
      <path ${pick('Torso')} class="${cls('Torso')}" d="M46 43 L65 37 L95 37 L114 43 L109 93 L98 115 L62 115 L51 93 Z"/>
      <path class="shade" d="M54 75 L73 78 L73 101 L63 107 L55 90 Z M87 78 L107 75 L105 90 L97 107 L87 101 Z"/>
      <path ${pick('Head')} class="${cls('Head')}" d="M65 11 L76 6 L90 11 L98 24 L94 42 L84 49 L69 42 L62 26 Z"/>
      <path class="sensor" d="M67 24 L91 24 L92 31 L69 32 Z"/>
      <path ${pick('Cockpit')} class="${cls('Cockpit')}" d="M72 53 L90 53 L96 67 L89 79 L72 79 L65 67 Z"/>
      <path d="M21 53 L38 51 M122 51 L138 53 M54 181 L66 179 M95 179 L107 181" stroke="#d2d6b6" stroke-opacity=".6" stroke-width="2"/>
    </svg>`;
  }
  function renderRoster() {
    $('roster-list').innerHTML = state.allies.map((u, i) => `<button class="roster-button ${state.selected === i ? 'selected' : ''}" data-unit="${i}" aria-pressed="${state.selected === i}"><span class="mini-mech">${mechSVG(u.hp)}</span><span class="roster-info"><strong>${u.name}</strong><small>${u.code}${u.ejected ? ' / EJECTED' : u.guard ? ' / GUARD' : ''}</small></span><span class="roster-ap">AP ${u.ap}/5${pips(u.ap)}</span></button>`).join('');
  }
  function renderMap() {
    const map = $('map');
    map.className = 'tactical-map ' + ({ move: 'move-mode', attack: 'attack-mode', overwatch: 'guard-mode' }[state.mode] || '');
    $('range-shape').setAttribute('d', state.mode === 'overwatch' ? 'M350 540 L130 100 Q400 -40 740 110 Z' : 'M420 95 L900 350 L420 605 L-60 350 Z');
    $('map-units').innerHTML = [ ...state.allies.map((u, i) => ({ u, i, enemy: false })), ...state.enemies.map((u, i) => ({ u, i, enemy: true })) ].map(({ u, i, enemy }) => `<button class="map-unit ${enemy ? 'enemy' : ''} ${!enemy && state.selected === i ? 'selected' : ''} ${enemy && state.target === i && state.mode === 'attack' ? 'targeted' : ''} ${status(u) === '전투불능' || u.ejected ? 'unit-disabled' : ''}" style="left:${u.x}%;top:${u.y}%" ${enemy ? `data-enemy="${i}"` : `data-unit="${i}"`} aria-label="${u.name}, ${status(u)}"><span class="footprint"></span><span class="unit-nameplate">${u.name}<small>${enemy ? `${status(u)} / ${u.cover}` : `${u.code} / AP ${u.ap}`}</small></span><span class="mech">${mechSVG(u.hp)}</span>${u.guard ? '<span class="guard-badge">경계</span>' : ''}</button>`).join('');
  }
  function renderUnitCard() {
    const u = current();
    if (!u) { $('unit-card').innerHTML = '<div class="empty-target"><span class="crosshair">⌖</span><h2>기체 선택</h2><p>전장의 아군 또는 소대 목록에서 행동할 기체를 선택하세요.</p></div>'; return; }
    $('unit-card').innerHTML = `<div class="card-heading"><div><h2>${u.name}</h2><p>파일럿 · ${u.pilot}</p></div><span class="unit-index">0${state.selected + 1} / 03</span></div><div class="unit-overview"><div class="unit-silhouette">${mechSVG(u.hp)}</div><div class="unit-metrics"><p class="status-value ${u.guard || u.ejected ? 'amber' : ''}">${u.ejected ? '탈출 완료' : u.guard ? '경계 중' : '기체 상태 · 정상'}</p><div><span class="label">ACTION POINTS</span><div class="ap-value">${u.ap} <span style="color:#738573;font-size:15px">/ 5</span></div>${pips(u.ap)}</div></div></div><div class="equipment-list">${weapons.map(w => `<div class="equipment-row"><span class="slot-tag">${w.slot}</span><span class="equipment-name">${w.name}${w.id === 'backpack' ? '<span class="equipment-note">재장전 불가</span>' : ''}</span><span class="ammo">${w.capacity ? `${u.ammo[w.id]} / ${w.capacity}` : '—'}</span></div>`).join('')}</div><div class="weight-readout"><span>장착 중량 18.6 / 24.0 t</span><span>LOAD 77.5%</span></div>`;
  }
  function hitChance() {
    if (!target()) return 0;
    const w = weapon();
    return Math.min(99, Math.max(0, Math.round(w.baseHit * (w.id === 'axe' ? 1 : target().modifier * parts.find(p => p.id === state.part).multiplier))));
  }
  function fireBlockReason() {
    const u = current(), t = target(), w = weapon();
    if (!canAct(u)) return '현재 행동할 수 없습니다';
    if (!t) return '공격 대상을 선택하세요';
    if (status(t) === '전투불능') return '전투불능 대상입니다';
    if (u.ap < w.ap) return 'AP가 부족합니다';
    if (w.capacity && u.ammo[w.id] <= 0) return '탄약이 없습니다';
    if (distance(u, t) > w.range) return '사거리 밖입니다';
    if (t.hp[state.part] <= 0) return '이미 파괴된 부위입니다';
    return '';
  }
  function renderTarget() {
    const panel = $('target-panel'), u = current(), w = weapon();
    panel.hidden = !u || !['attack', 'overwatch', 'reload'].includes(state.mode);
    if (panel.hidden) return;
    if (state.mode === 'overwatch') {
      panel.innerHTML = `<div class="target-heading"><div><h2>경계 설정</h2><p>OVERWATCH / FRONT SECTOR</p></div><span class="tag" style="color:var(--amber)">90°</span></div><div class="empty-target"><span class="crosshair">◷</span><h2>전방을 경계합니다</h2><p>${w.name}<br>전방 90° · 무기 사거리 ${w.range}칸<br>진입 적 발견 시 사격 여부 선택<br>경계 명중률 ×0.75</p><p>시연에서는 경계 상태 표시까지 제공합니다.</p></div><div class="modifiers">설정 비용 2 AP · 재장전 불가 백팩도 선택 가능</div><div class="fire-row"><button class="fire-button" id="confirm-guard" ${!canAct(u) || u.ap < 2 || w.id === 'axe' || u.ammo[w.id] < 1 ? 'disabled' : ''}>경계 확정 <small>GUARD</small></button><button class="cancel-button" data-cancel>취소</button></div>`; return;
    }
    if (state.mode === 'reload') {
      panel.innerHTML = `<div class="target-heading"><div><h2>재장전</h2><p>MAGAZINE REPLACEMENT</p></div></div><div class="empty-target"><span class="crosshair">↻</span><h2>${w.name}</h2><p>잔탄 ${u.ammo.rifle} / 6<br>예비 에너지 팩 ${u.spares}개<br>남은 탄약은 버리고 새 팩으로 교체합니다.</p></div><div class="modifiers">백팩 무장은 재장전할 수 없습니다</div><div class="fire-row"><button class="fire-button" id="confirm-reload" ${!canAct(u) || u.ap < 2 || u.spares === 0 || u.ammo.rifle === 6 ? 'disabled' : ''}>교체 확정 <small>2 AP</small></button><button class="cancel-button" data-cancel>취소</button></div>`; return;
    }
    const t = target();
    if (!t) { panel.innerHTML = '<div class="empty-target"><span class="crosshair">⌖</span><h2>공격 대상 선택</h2><p>전장의 적 기체를 클릭하면<br>부위별 예상 명중률을 확인할 수 있습니다.</p></div>'; return; }
    const part = parts.find(p => p.id === state.part), reason = fireBlockReason();
    panel.innerHTML = `<div class="target-heading"><div><h2>${w.id === 'axe' ? '근접 공격' : '부위 조준'}</h2><p>${t.name} · DIST ${distance(u, t)}</p></div><span class="tag">HOSTILE</span></div><div class="target-diagram"><span class="diagram-corner"></span>${mechSVG(t.hp, state.part)}<span class="diagram-corner bottom"></span></div><div class="part-grid">${parts.map(p => `<button class="part-button ${state.part === p.id ? 'active' : ''}" data-part="${p.id}" aria-pressed="${state.part === p.id}" ${t.hp[p.id] <= 0 || w.id === 'axe' ? 'disabled' : ''}>${p.name}${t.hp[p.id] <= 0 ? ' ×' : ''}</button>`).join('')}</div><div class="selected-part"><strong>${w.id === 'axe' ? '자동 부위 선택' : part.name}</strong><span>HP ${t.hp[state.part]} / ${part.hp}</span></div><div class="hit-readout"><span>예상 명중률</span><strong>${hitChance()}<small>%</small></strong></div><dl class="preview-rows"><div><dt>예상 피해</dt><dd>${w.min}–${w.max}</dd></div><div><dt>치명타</dt><dd>5% / ×1.25</dd></div><div><dt>소모 AP</dt><dd>${w.ap}</dd></div><div><dt>탄약 소모</dt><dd>${w.capacity ? '1' : '없음'}</dd></div></dl><div class="modifiers">${reason || (w.id === 'axe' ? '엄폐 무시 · 부위 자동 선택' : `${t.cover} · 일반 장갑 · 부위 ×${part.multiplier}`)}</div><div class="fire-row"><button id="fire" class="fire-button" ${reason ? 'disabled' : ''}>${w.id === 'axe' ? '공격' : '사격'} <small>FIRE</small></button><button class="cancel-button" data-cancel>취소</button></div>`;
  }
  function renderCommands() {
    const u = current();
    const showWeapons = u && ['attack', 'overwatch', 'reload'].includes(state.mode);
    $('weapon-menu').hidden = !showWeapons;
    $('weapon-menu').innerHTML = showWeapons ? weapons.filter(w => state.mode === 'reload' ? w.reload : state.mode === 'overwatch' ? w.capacity : true).map(w => `<button class="weapon-option ${state.weapon === w.id ? 'active' : ''}" data-weapon="${w.id}" aria-pressed="${state.weapon === w.id}" ${!canAct(u) ? 'disabled' : ''}><span class="slot-tag">${w.slot}</span><strong>${w.name}</strong><small>${w.capacity ? `${u.ammo[w.id]}/${w.capacity} · ` : ''}${w.ap} AP · ${w.range}칸</small></button>`).join('') : '';
    const hints = { move: '전장의 빈 지점을 클릭해 이동 · 이동 거리에 따라 AP 소모', attack: `${weapon().category} · 적 기체 선택 → 부위 선택 → FIRE`, overwatch: '원거리 무기를 선택한 후 경계를 확정하세요', reload: '손 무기만 재장전 가능 · 남은 탄약 폐기' };
    $('mode-hint').textContent = !u ? '소대 목록 또는 전장에서 아군 기체를 선택하세요' : u.ejected ? '파일럿 탈출 완료 · 이번 시연에서 기체 사용 불가' : u.guard ? '경계 중 · 다음 아군 턴에 해제됩니다' : hints[state.mode] || '행동을 선택하세요 · 이동 / 공격 / 경계 / 재장전';
    root.querySelectorAll('[data-action]').forEach(b => { b.classList.toggle('active', b.dataset.action === state.mode); b.setAttribute('aria-pressed', String(b.dataset.action === state.mode)); b.disabled = b.dataset.action === 'eject' ? !u || u.ejected || u.ap < 1 || state.phase !== 'player' : !canAct(u) || u.ap < (['overwatch', 'reload'].includes(b.dataset.action) ? 2 : b.dataset.action === 'attack' ? 3 : 1); });
  }
  function render() {
    renderRoster(); renderMap(); renderUnitCard(); renderCommands(); renderTarget();
    $('turn-label').textContent = state.phase === 'player' ? '아군 턴' : '적군 턴';
    $('turn-number').textContent = String(state.turn).padStart(2, '0');
    $('end-turn').disabled = state.phase !== 'player';
    $('objective-progress').textContent = `${state.enemies.filter(e => status(e) === '전투불능').length} / 3`;
    $('combat-feed').innerHTML = state.logs.slice(-3).map(text => `<div class="feed-line">${text}</div>`).join('');
  }
  function toast(message) { clearTimeout(toastTimer); $('toast').textContent = message; $('toast').classList.add('visible'); toastTimer = setTimeout(() => $('toast').classList.remove('visible'), 3000); }
  function log(message) { state.logs.push(message); }
  function selectUnit(index) {
    if (state.phase !== 'player') return;
    state.selected = index; state.mode = 'idle'; state.target = null; state.weapon = 'rifle'; state.part = 'Torso'; render();
  }
  function chooseAction(action) {
    const u = current(); if (!u || state.phase !== 'player') return;
    if (action === 'eject') { if (u.ap >= 1 && !u.ejected) { $('confirm-dialog').returnValue = 'cancel'; $('confirm-dialog').showModal(); } return; }
    if (!canAct(u)) return;
    state.mode = action; state.target = null;
    if (action === 'reload' || action === 'overwatch' && state.weapon === 'axe') state.weapon = 'rifle';
    render();
  }
  function fire() {
    const reason = fireBlockReason(); if (reason) return toast(reason);
    const u = current(), t = target(), w = weapon();
    const hit = Math.random() * 100 < hitChance(), critical = hit && Math.random() < .05;
    u.ap -= w.ap; if (w.capacity) u.ammo[w.id]--;
    let hitPart = state.part;
    if (w.id === 'axe') { const available = parts.filter(p => t.hp[p.id] > 0); hitPart = available[Math.floor(Math.random() * available.length)].id; }
    const damage = hit ? Math.round((w.min + Math.random() * (w.max - w.min)) * (critical ? 1.25 : 1)) : 0;
    if (hit) t.hp[hitPart] = Math.max(0, t.hp[hitPart] - damage);
    const result = hit ? `${critical ? '치명타! ' : ''}${parts.find(p => p.id === hitPart).name} ${damage} 피해` : '빗나감';
    log(`${u.name} → ${t.name} · ${result}`); toast(`${result} · AP −${w.ap}${w.capacity ? ' / 탄약 −1' : ''}`);
    render();
    const flash = document.createElement('div'); flash.className = 'shot-flash'; $('map').appendChild(flash); setTimeout(() => flash.remove(), 450);
  }
  // Delegate clicks so state updates can redraw panels without losing their bindings.
  root.addEventListener('click', event => {
    const unitButton = event.target.closest('[data-unit]'); if (unitButton) { selectUnit(Number(unitButton.dataset.unit)); return; }
    const enemyButton = event.target.closest('[data-enemy]');
    if (enemyButton) { if (!canAct(current())) return toast('행동 가능한 아군을 먼저 선택하세요'); const index = Number(enemyButton.dataset.enemy); if (status(state.enemies[index]) === '전투불능') return toast('전투불능 대상입니다'); state.target = index; state.mode = 'attack'; state.part = 'Torso'; render(); return; }
    const actionButton = event.target.closest('[data-action]'); if (actionButton && !actionButton.disabled) { chooseAction(actionButton.dataset.action); return; }
    const weaponButton = event.target.closest('[data-weapon]'); if (weaponButton && !weaponButton.disabled) { state.weapon = weaponButton.dataset.weapon; if (state.weapon === 'axe') state.part = 'Torso'; render(); return; }
    const partButton = event.target.closest('[data-part]'); if (partButton && !partButton.disabled && weapon().id !== 'axe' && target() && target().hp[partButton.dataset.part] > 0) { state.part = partButton.dataset.part; renderTarget(); return; }
    if (event.target.closest('[data-cancel]')) { state.mode = 'idle'; state.target = null; render(); return; }
    if (event.target.closest('#fire')) { fire(); return; }
    if (event.target.closest('#confirm-guard')) { const u = current(), w = weapon(); if (!canAct(u) || u.ap < 2 || !w.capacity || !u.ammo[w.id]) return; u.ap -= 2; u.guard = true; state.mode = 'idle'; log(`${u.name} · ${w.name} 경계 설정`); toast('경계 설정 완료 · AP −2'); render(); return; }
    if (event.target.closest('#confirm-reload')) { const u = current(); if (!canAct(u) || u.ap < 2 || !u.spares || u.ammo.rifle === 6) return; u.ap -= 2; u.spares--; u.ammo.rifle = 6; state.mode = 'idle'; toast('에너지 팩 교체 완료 · AP −2 / 예비 팩 −1'); log(`${u.name} · 빔 라이플 재장전`); render(); return; }
    if (event.target.closest('#end-turn')) { if (state.phase !== 'player') return; state.phase = 'enemy'; state.mode = 'idle'; state.target = null; toast('적군 턴 시연 · 잠시 후 아군 AP를 회복합니다'); render(); phaseTimer = setTimeout(() => { state.phase = 'player'; state.turn++; state.allies.forEach(u => { if (!u.ejected) { u.ap = 5; u.guard = false; } }); log(`TURN ${state.turn} · 아군 행동 시작`); toast('새 아군 턴 · AP 회복 / 경계 해제'); render(); }, 1600); return; }
    if (event.target.closest('#reset')) { clearTimeout(phaseTimer); clearTimeout(toastTimer); state = initialState(); $('toast').classList.remove('visible'); root.querySelector('.terrain').style.transform = ''; render(); return; }
    if (event.target.closest('#map')) {
      const u = current();
      if (state.mode !== 'move' || !canAct(u)) { state.selected = null; state.mode = 'idle'; state.target = null; render(); return; }
      const r = $('map').getBoundingClientRect();
      const point = { x: Math.min(94, Math.max(6, (event.clientX - r.left) / r.width * 100)), y: Math.min(87, Math.max(12, (event.clientY - r.top) / r.height * 100)) };
      const cost = distance(u, point);
      if (cost > u.ap) return toast(`AP 부족 · 이 지점까지 ${cost} AP 필요`);
      const occupied = [...state.allies, ...state.enemies].some(v => v !== u && !v.ejected && status(v) !== '전투불능' && Math.hypot(v.x - point.x, v.y - point.y) < 9);
      if (occupied) return toast('다른 기체가 점유한 지점입니다');
      u.x = point.x; u.y = point.y; u.ap -= cost; state.mode = 'idle'; log(`${u.name} · 이동 / AP −${cost}`); toast(`이동 완료 · AP −${cost} · 지형 판정은 시연에서 생략`); render();
    }
  });
  $('confirm-dialog').addEventListener('close', () => { if ($('confirm-dialog').returnValue !== 'eject') return; const u = current(); if (!u || u.ejected || u.ap < 1) return; u.ap--; u.ejected = true; u.guard = false; state.mode = 'idle'; log(`${u.name} · ${u.pilot} 긴급탈출`); toast('파일럿 탈출 완료 · 기체 행동 불가'); render(); });
  document.addEventListener('keydown', event => { if ($('confirm-dialog').open || event.key !== 'Escape') return; state.selected = null; state.mode = 'idle'; state.target = null; render(); toast('기체 선택 해제'); });
  $('map').addEventListener('wheel', event => { event.preventDefault(); state.zoom = Math.min(1.35, Math.max(1, state.zoom + (event.deltaY < 0 ? .05 : -.05))); root.querySelector('.terrain').style.transform = `scale(${state.zoom})`; }, { passive: false });
  state = initialState(); render();
})();
