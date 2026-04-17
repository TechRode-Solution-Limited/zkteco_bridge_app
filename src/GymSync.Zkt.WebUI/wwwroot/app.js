const $ = (id) => document.getElementById(id);

// Populate device inputs from server config.
async function loadConfig() {
  const r = await fetch("/api/config");
  const j = await r.json();
  const d = j.device;
  $("cfg-ip").value = d.ip;
  $("cfg-port").value = d.port;
  $("cfg-password").value = d.password ?? 0;
  $("cfg-timeout").value = d.timeout ?? 10;
  $("cfg-machine").value = d.machineNumber ?? 1;
  $("device-addr").textContent = `${d.ip}:${d.port}`;
}

function deviceOverrides() {
  return {
    ip: $("cfg-ip").value.trim(),
    port: parseInt($("cfg-port").value, 10),
    password: parseInt($("cfg-password").value, 10) || 0,
    timeout: parseInt($("cfg-timeout").value, 10) || 10,
    machineNumber: parseInt($("cfg-machine").value, 10) || 1,
  };
}

function show(elId, ok, payload) {
  const el = $(elId);
  el.classList.remove("ok", "err");
  el.classList.add(ok ? "ok" : "err");
  el.textContent = typeof payload === "string" ? payload : JSON.stringify(payload, null, 2);
}

async function callJson(path, body) {
  const res = await fetch(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body ?? {}),
  });
  const j = await res.json().catch(() => ({ ok: false, error: `HTTP ${res.status}` }));
  return { ok: res.ok && j.ok !== false, body: j };
}

async function runAction(action, btn) {
  btn.disabled = true;
  try {
    if (action === "list")     await doList();
    if (action === "download") await doDownload();
    if (action === "upload")   await doUpload();
    if (action === "storage")  await doStorage();
  } finally {
    btn.disabled = false;
  }
}

async function doList() {
  show("out-list", true, "Fetching users…");
  const { ok, body } = await callJson("/api/users", deviceOverrides());
  if (!ok) return show("out-list", false, body.error || body);

  const rows = body.users.map((u) =>
    `<tr><td>${u.enrollNumber}</td><td>${u.name || ""}</td><td>${u.privilege}</td><td>${u.enabled ? "yes" : "no"}</td></tr>`
  ).join("");

  const el = $("out-list");
  el.classList.remove("err"); el.classList.add("ok");
  el.innerHTML = `<table>
    <thead><tr><th>Enroll #</th><th>Name</th><th>Privilege</th><th>Enabled</th></tr></thead>
    <tbody>${rows || '<tr><td colspan="4">No users</td></tr>'}</tbody>
  </table>`;
}

async function doDownload() {
  show("out-download", true, "Downloading templates…");
  const body = {
    ...deviceOverrides(),
    enrollNumber: $("dl-enroll").value.trim(),
    all: $("dl-all").checked,
  };
  const { ok, body: resp } = await callJson("/api/download", body);
  show("out-download", ok, resp);
}

async function doUpload() {
  show("out-upload", true, "Uploading templates…");
  const body = {
    ...deviceOverrides(),
    enrollNumber: $("up-enroll").value.trim(),
    sourceIp: $("up-source-ip").value.trim(),
    targetEnrollNumber: $("up-target-enroll").value.trim(),
    skipFingers: $("up-skip-fingers").checked,
    skipFaces: $("up-skip-faces").checked,
  };
  const { ok, body: resp } = await callJson("/api/upload", body);
  show("out-upload", ok, resp);
}

async function doStorage() {
  show("out-storage", true, "Reading storage…");
  const r = await fetch("/api/storage");
  const j = await r.json();
  if (!j.ok) return show("out-storage", false, j);
  if (!j.items.length) return show("out-storage", true, "(no downloaded templates yet)");

  const rows = j.items.map((it) =>
    `<tr><td>${it.deviceIp}</td><td>${it.enrollNumber}</td><td>${it.name || ""}</td><td>${it.fingers}</td><td>${it.faces}</td><td>${it.downloadedAt || ""}</td></tr>`
  ).join("");

  const el = $("out-storage");
  el.classList.remove("err"); el.classList.add("ok");
  el.innerHTML = `<table>
    <thead><tr><th>Device IP</th><th>Enroll #</th><th>Name</th><th>Fingers</th><th>Faces</th><th>Downloaded</th></tr></thead>
    <tbody>${rows}</tbody>
  </table>`;
}

document.addEventListener("click", (e) => {
  const btn = e.target.closest("button[data-action]");
  if (btn) runAction(btn.dataset.action, btn);
});

loadConfig();
