(() => {
  "use strict";

  const sidebar = document.getElementById("sidebar");
  const toggle = document.getElementById("sidebar-toggle");
  const collapse = document.getElementById("sidebar-collapse");
  const overlay = document.getElementById("loading-overlay");

  toggle?.addEventListener("click", () => {
    sidebar?.classList.toggle("open");
    const open = sidebar?.classList.contains("open") ?? false;
    toggle.setAttribute("aria-expanded", String(open));
  });

  collapse?.addEventListener("click", () => {
    sidebar?.classList.toggle("collapsed");
  });

  document.querySelectorAll("a.nav-link, a.nav-sublink").forEach((link) => {
    link.addEventListener("click", () => {
      if (window.innerWidth < 992) {
        sidebar?.classList.remove("open");
        toggle?.setAttribute("aria-expanded", "false");
      }
      overlay?.classList.remove("d-none");
    });
  });

  document.querySelector("#global-filters")?.addEventListener("submit", () => {
    overlay?.classList.remove("d-none");
  });

  window.addEventListener("load", () => {
    overlay?.classList.add("d-none");
    renderSparklines();
  });

  function chartDefaults() {
    return {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { display: false } },
      scales: {
        x: { ticks: { maxRotation: 45, minRotation: 0 } },
        y: { beginAtZero: true }
      }
    };
  }

  function renderLineChart(canvasId, points, color) {
    const el = document.getElementById(canvasId);
    if (!el || typeof Chart === "undefined") return;
    const labels = (points || []).map((p) => p.PeriodLabel || p.periodLabel);
    const values = (points || []).map((p) => p.Value ?? p.value ?? 0);
    new Chart(el, {
      type: "line",
      data: {
        labels,
        datasets: [{
          data: values,
          borderColor: color || "#1a6f9a",
          backgroundColor: "rgba(26,111,154,0.08)",
          borderWidth: 2,
          pointRadius: 2,
          pointHoverRadius: 4,
          tension: 0.25,
          fill: true
        }]
      },
      options: chartDefaults()
    });
  }

  function renderBarChart(canvasId, items, horizontal) {
    const el = document.getElementById(canvasId);
    if (!el || typeof Chart === "undefined") return;
    const labels = (items || []).map((p) => p.Label || p.label);
    const values = (items || []).map((p) => p.Value ?? p.value ?? 0);
    new Chart(el, {
      type: horizontal ? "bar" : "bar",
      data: {
        labels,
        datasets: [{
          data: values,
          backgroundColor: "#4db8e8"
        }]
      },
      options: {
        ...chartDefaults(),
        indexAxis: horizontal ? "y" : "x"
      }
    });
  }

  const PIE_COLORS = [
    "#1a6f9a", "#e67e22", "#1e8449", "#8e44ad", "#c0392b",
    "#16a085", "#2980b9", "#f1c40f", "#d35400", "#27ae60",
    "#9b59b6", "#e74c3c", "#2c3e50", "#1abc9c", "#e91e63"
  ];

  function slicePieItems(items, maxSlices) {
    const list = (items || []).slice();
    if (!maxSlices || list.length <= maxSlices) return list;
    const keep = Math.max(1, maxSlices - 1);
    const head = list.slice(0, keep);
    const restValue = list.slice(keep).reduce((sum, p) => sum + (Number(p.Value ?? p.value) || 0), 0);
    if (restValue > 0) head.push({ Label: "Other", Value: restValue });
    return head;
  }

  function isLightColor(hex) {
    if (!hex || typeof hex !== "string" || !hex.startsWith("#") || hex.length < 7) return false;
    const r = parseInt(hex.slice(1, 3), 16);
    const g = parseInt(hex.slice(3, 5), 16);
    const b = parseInt(hex.slice(5, 7), 16);
    return ((r * 299) + (g * 587) + (b * 114)) / 1000 > 155;
  }

  const pieSliceLabels = {
    id: "pieSliceLabels",
    afterDatasetsDraw(chart) {
      if (chart.config.type !== "pie") return;
      const { ctx } = chart;
      const meta = chart.getDatasetMeta(0);
      const data = chart.data.datasets[0]?.data || [];
      const colors = chart.data.datasets[0]?.backgroundColor || [];
      const total = data.reduce((sum, v) => sum + Number(v || 0), 0) || 1;
      ctx.save();
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      meta.data.forEach((arc, i) => {
        const value = Number(data[i]) || 0;
        const pct = (value / total) * 100;
        if (pct < 4) return;
        const pos = typeof arc.tooltipPosition === "function" ? arc.tooltipPosition() : arc.getCenterPoint();
        ctx.fillStyle = isLightColor(colors[i]) ? "#132033" : "#ffffff";
        ctx.shadowColor = isLightColor(colors[i]) ? "rgba(255,255,255,0.7)" : "rgba(0,0,0,0.45)";
        ctx.shadowBlur = 2;
        ctx.font = "600 11px 'Segoe UI', system-ui, sans-serif";
        ctx.fillText(value.toLocaleString(), pos.x, pos.y - 7);
        ctx.font = "10px 'Segoe UI', system-ui, sans-serif";
        ctx.fillText(`${pct.toFixed(1)}%`, pos.x, pos.y + 7);
      });
      ctx.restore();
    }
  };

  function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>"']/g, (ch) => ({
      "&": "&amp;",
      "<": "&lt;",
      ">": "&gt;",
      '"': "&quot;",
      "'": "&#39;"
    }[ch]));
  }

  function renderHtmlLegend(canvasEl, labels, colors) {
    const legend = canvasEl.closest(".card-body")?.querySelector(".chart-legend");
    if (!legend) return;
    legend.innerHTML = labels.map((label, i) =>
      `<span class="chart-legend-item"><span class="chart-legend-swatch" style="background:${colors[i]}"></span>${escapeHtml(label)}</span>`
    ).join("");
  }

  function renderPieChart(canvasId, items, maxSlices) {
    const el = document.getElementById(canvasId);
    if (!el || typeof Chart === "undefined") return;
    const sliced = slicePieItems(items, maxSlices);
    const labels = sliced.map((p) => p.Label || p.label);
    const values = sliced.map((p) => Number(p.Value ?? p.value) || 0);
    const colors = labels.map((label, i) =>
      label === "Other" ? "#94a3b8" : PIE_COLORS[i % PIE_COLORS.length]);
    renderHtmlLegend(el, labels, colors);
    new Chart(el, {
      type: "pie",
      data: {
        labels,
        datasets: [{
          data: values,
          backgroundColor: colors,
          borderColor: "#ffffff",
          borderWidth: 1
        }]
      },
      plugins: [pieSliceLabels],
      options: {
        responsive: true,
        maintainAspectRatio: false,
        layout: { padding: 8 },
        plugins: {
          legend: { display: false },
          tooltip: {
            callbacks: {
              label(ctx) {
                const total = (ctx.dataset.data || []).reduce((a, b) => a + b, 0) || 1;
                const value = ctx.parsed;
                const pct = ((value / total) * 100).toFixed(1);
                return ` ${ctx.label}: ${Number(value).toLocaleString()} (${pct}%)`;
              }
            }
          }
        }
      }
    });
  }

  function renderDashboardCharts(data) {
    renderLineChart("chart-activations", data.activations, "#1a6f9a");
    renderLineChart("chart-cancellations", data.cancellations, "#c0392b");
    renderLineChart("chart-net", data.net, "#1e8449");
    renderPieChart("chart-status", data.status, 8);
    renderPieChart("chart-region", data.region, 8);
    renderPieChart("chart-product", data.product, 8);
    renderPieChart("chart-reasons", data.reasons, 8);
  }

  function bindKpiSqlModal(queries) {
    const modal = document.getElementById("kpi-sql-modal");
    if (!modal) return;

    const titleEl = document.getElementById("kpi-sql-modal-title");
    const notesEl = document.getElementById("kpi-sql-modal-notes");
    const codeEl = document.getElementById("kpi-sql-modal-code");
    const copyBtn = document.getElementById("kpi-sql-copy");
    let currentSql = "";

    modal.addEventListener("show.bs.modal", (event) => {
      const button = event.relatedTarget;
      if (!(button instanceof HTMLElement)) return;
      const key = button.getAttribute("data-kpi-key");
      const entry = (queries && key) ? (queries[key] || null) : null;
      const title = entry?.title || entry?.Title || key || "KPI SQL";
      const notes = entry?.notes || entry?.Notes || "";
      currentSql = entry?.sql || entry?.Sql || "";
      if (titleEl) titleEl.textContent = title;
      if (notesEl) notesEl.textContent = notes;
      if (codeEl) codeEl.textContent = currentSql;
    });

    copyBtn?.addEventListener("click", async () => {
      if (!currentSql) return;
      try {
        await navigator.clipboard.writeText(currentSql);
        copyBtn.textContent = "Copied";
        setTimeout(() => { copyBtn.textContent = "Copy SQL"; }, 1500);
      } catch {
        copyBtn.textContent = "Copy failed";
        setTimeout(() => { copyBtn.textContent = "Copy SQL"; }, 1500);
      }
    });
  }

  function bindKpiChartModal(charts) {
    const modal = document.getElementById("kpi-chart-modal");
    if (!modal || typeof Chart === "undefined") return;

    const titleEl = document.getElementById("kpi-chart-modal-title");
    const notesEl = document.getElementById("kpi-chart-modal-notes");
    const emptyEl = document.getElementById("kpi-chart-modal-empty");
    const canvas = document.getElementById("kpi-chart-modal-canvas");
    let chartInstance = null;

    modal.addEventListener("show.bs.modal", (event) => {
      const button = event.relatedTarget;
      if (!(button instanceof HTMLElement) || !(canvas instanceof HTMLCanvasElement)) return;
      const key = button.getAttribute("data-kpi-chart-key");
      const fallbackTitle = button.getAttribute("data-kpi-chart-title") || key || "KPI Chart";
      const entry = (charts && key) ? (charts[key] || charts[key?.toLowerCase?.()] || null) : null;
      const title = entry?.title || entry?.Title || fallbackTitle;
      const notes = entry?.note || entry?.Notes || "";
      const points = entry?.points || entry?.Points || [];
      const color = entry?.color || entry?.Color || "#1a6f9a";

      if (titleEl) titleEl.textContent = title;
      if (notesEl) notesEl.textContent = notes;

      if (chartInstance) {
        chartInstance.destroy();
        chartInstance = null;
      }

      const hasData = Array.isArray(points) && points.length > 0;
      if (emptyEl) emptyEl.hidden = hasData;
      canvas.style.display = hasData ? "" : "none";
      if (!hasData) return;

      const labels = points.map((p) => p.label || p.Label || p.PeriodLabel || p.periodLabel || "");
      const values = points.map((p) => Number(p.value ?? p.Value ?? 0));

      chartInstance = new Chart(canvas, {
        type: "line",
        data: {
          labels,
          datasets: [{
            data: values,
            borderColor: color,
            backgroundColor: "rgba(26,111,154,0.12)",
            tension: 0.25,
            fill: true
          }]
        },
        options: {
          ...chartDefaults(),
          plugins: { legend: { display: false } }
        }
      });
    });

    modal.addEventListener("hidden.bs.modal", () => {
      if (chartInstance) {
        chartInstance.destroy();
        chartInstance = null;
      }
    });
  }

  function readGlobalFilterParams() {
    const form = document.getElementById("global-filters");
    const params = new URLSearchParams();
    if (!form) return params;
    const fd = new FormData(form);
    for (const [name, value] of fd.entries()) {
      if (value === null || value === undefined) continue;
      const text = String(value).trim();
      if (!text && name !== "IncludeTestAccounts") continue;
      if (name === "IncludeTestAccounts") {
        if (text) params.set(name, "true");
        continue;
      }
      params.set(name, text);
    }
    const include = form.querySelector("#IncludeTestAccounts");
    if (include instanceof HTMLInputElement && include.checked) {
      params.set("IncludeTestAccounts", "true");
    }
    return params;
  }

  function escapeHtml(value) {
    return String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function bindKpiLinesModal() {
    const modal = document.getElementById("kpi-lines-modal");
    if (!modal) return;

    const titleEl = document.getElementById("kpi-lines-modal-title");
    const searchEl = document.getElementById("kpi-lines-search");
    const searchBtn = document.getElementById("kpi-lines-search-btn");
    const exportLink = document.getElementById("kpi-lines-export-csv");
    const metaEl = document.getElementById("kpi-lines-meta");
    const thead = document.getElementById("kpi-lines-thead");
    const tbody = document.getElementById("kpi-lines-tbody");
    const prevBtn = document.getElementById("kpi-lines-prev");
    const nextBtn = document.getElementById("kpi-lines-next");
    const pageInfo = document.getElementById("kpi-lines-page-info");

    let currentKey = "";
    let currentTitle = "";
    let currentPage = 1;
    let pageSize = 50;
    let totalCount = 0;
    let csvPath = "";
    let loading = false;

    function buildQuery(page) {
      const params = readGlobalFilterParams();
      const search = (searchEl?.value || "").trim();
      if (search) params.set("Search", search);
      else params.delete("Search");
      params.set("Page", String(page));
      params.set("PageSize", String(pageSize));
      return params;
    }

    function updateExportLink() {
      if (!(exportLink instanceof HTMLAnchorElement) || !csvPath) {
        if (exportLink) exportLink.setAttribute("href", "#");
        return;
      }
      const params = buildQuery(1);
      params.delete("Page");
      params.delete("PageSize");
      exportLink.href = `${csvPath}?${params.toString()}`;
    }

    function renderEmpty(message) {
      if (thead) thead.innerHTML = "";
      if (tbody) tbody.innerHTML = `<tr><td class="text-muted">${escapeHtml(message)}</td></tr>`;
      if (pageInfo) pageInfo.textContent = "";
      if (prevBtn) prevBtn.disabled = true;
      if (nextBtn) nextBtn.disabled = true;
    }

    function renderPage(data) {
      const columns = data.columns || data.Columns || [];
      const rows = data.rows || data.Rows || [];
      totalCount = data.totalCount ?? data.TotalCount ?? 0;
      currentPage = data.page ?? data.Page ?? currentPage;
      pageSize = data.pageSize ?? data.PageSize ?? pageSize;
      csvPath = data.csvExportPath || data.CsvExportPath || csvPath;
      const title = data.title || data.Title || currentTitle;
      if (titleEl) titleEl.textContent = title;
      if (metaEl) {
        metaEl.textContent = totalCount
          ? `${totalCount.toLocaleString()} matching line(s) · page ${currentPage} of ${Math.max(1, Math.ceil(totalCount / pageSize))}`
          : "No matching lines for the current filters.";
      }

      if (thead) {
        thead.innerHTML = `<tr>${columns.map((c) => `<th scope="col">${escapeHtml(c)}</th>`).join("")}</tr>`;
      }
      if (tbody) {
        if (!rows.length) {
          tbody.innerHTML = `<tr><td colspan="${Math.max(columns.length, 1)}" class="text-muted">No rows found.</td></tr>`;
        } else {
          tbody.innerHTML = rows.map((row) =>
            `<tr>${(row || []).map((cell) => `<td>${escapeHtml(cell)}</td>`).join("")}</tr>`
          ).join("");
        }
      }

      const maxPage = Math.max(1, Math.ceil(totalCount / pageSize));
      if (pageInfo) pageInfo.textContent = `Page ${currentPage} / ${maxPage}`;
      if (prevBtn) prevBtn.disabled = currentPage <= 1 || loading;
      if (nextBtn) nextBtn.disabled = currentPage >= maxPage || loading;
      updateExportLink();
    }

    async function load(page) {
      if (!currentKey || loading) return;
      loading = true;
      if (prevBtn) prevBtn.disabled = true;
      if (nextBtn) nextBtn.disabled = true;
      renderEmpty("Loading…");
      try {
        const params = buildQuery(page);
        const res = await fetch(`/api/dashboard/kpi-lines/${encodeURIComponent(currentKey)}?${params.toString()}`, {
          headers: { Accept: "application/json" }
        });
        if (!res.ok) {
          const err = await res.json().catch(() => null);
          throw new Error(err?.error || `Failed to load lines (${res.status})`);
        }
        const data = await res.json();
        renderPage(data);
      } catch (err) {
        renderEmpty(err?.message || "Failed to load related lines.");
        if (metaEl) metaEl.textContent = "Unable to load lines for this KPI.";
      } finally {
        loading = false;
        const maxPage = Math.max(1, Math.ceil(totalCount / pageSize));
        if (prevBtn) prevBtn.disabled = currentPage <= 1;
        if (nextBtn) nextBtn.disabled = currentPage >= maxPage || totalCount === 0;
      }
    }

    modal.addEventListener("show.bs.modal", (event) => {
      const button = event.relatedTarget;
      if (!(button instanceof HTMLElement)) return;
      currentKey = button.getAttribute("data-kpi-lines-key") || "";
      currentTitle = button.getAttribute("data-kpi-lines-title") || "Related Lines";
      if (titleEl) titleEl.textContent = currentTitle;
      if (searchEl) searchEl.value = "";
      currentPage = 1;
      totalCount = 0;
      csvPath = "";
      load(1);
    });

    searchBtn?.addEventListener("click", () => load(1));
    searchEl?.addEventListener("keydown", (e) => {
      if (e.key === "Enter") {
        e.preventDefault();
        load(1);
      }
    });
    prevBtn?.addEventListener("click", () => {
      if (currentPage > 1) load(currentPage - 1);
    });
    nextBtn?.addEventListener("click", () => {
      const maxPage = Math.max(1, Math.ceil(totalCount / pageSize));
      if (currentPage < maxPage) load(currentPage + 1);
    });
  }

  function renderSparklines() {
    document.querySelectorAll("svg.kpi-sparkline[data-spark]").forEach((svg) => {
      let values = [];
      try { values = JSON.parse(svg.getAttribute("data-spark") || "[]"); } catch { values = []; }
      if (!Array.isArray(values) || values.length < 2) return;
      const w = 64;
      const h = 18;
      const min = Math.min(...values);
      const max = Math.max(...values);
      const span = max - min || 1;
      const pts = values.map((v, i) => {
        const x = (i / (values.length - 1)) * w;
        const y = h - 2 - ((v - min) / span) * (h - 4);
        return `${x.toFixed(1)},${y.toFixed(1)}`;
      });
      const path = document.createElementNS("http://www.w3.org/2000/svg", "path");
      path.setAttribute("d", `M ${pts.join(" L ")}`);
      svg.replaceChildren(path);
    });
  }

  function stampFileName(ext) {
    const now = new Date();
    const pad = (n) => String(n).padStart(2, "0");
    return `iceboard-dashboard-${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}-${pad(now.getHours())}${pad(now.getMinutes())}.${ext}`;
  }

  async function captureDashboardCanvas() {
    const root = document.getElementById("dashboard-snapshot");
    if (!root) throw new Error("Dashboard snapshot area was not found.");
    if (typeof html2canvas !== "function") throw new Error("Screenshot library failed to load.");

    root.classList.add("dashboard-capturing");
    try {
      // Let layout settle (hide export chrome) before rasterizing.
      await new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r)));
      return await html2canvas(root, {
        backgroundColor: "#f4f7fb",
        scale: Math.min(2, window.devicePixelRatio || 2),
        useCORS: true,
        logging: false,
        scrollX: 0,
        scrollY: -window.scrollY,
        windowWidth: document.documentElement.clientWidth
      });
    } finally {
      root.classList.remove("dashboard-capturing");
    }
  }

  function downloadDataUrl(dataUrl, fileName) {
    const a = document.createElement("a");
    a.href = dataUrl;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
  }

  async function exportDashboardPng() {
    const canvas = await captureDashboardCanvas();
    downloadDataUrl(canvas.toDataURL("image/png"), stampFileName("png"));
  }

  async function exportDashboardPdf() {
    const canvas = await captureDashboardCanvas();
    const jsPdfNs = window.jspdf;
    if (!jsPdfNs?.jsPDF) throw new Error("PDF library failed to load.");

    const imgData = canvas.toDataURL("image/png");
    const pdf = new jsPdfNs.jsPDF({
      orientation: canvas.width >= canvas.height ? "landscape" : "portrait",
      unit: "pt",
      format: "a4",
      compress: true
    });

    const pageWidth = pdf.internal.pageSize.getWidth();
    const pageHeight = pdf.internal.pageSize.getHeight();
    const margin = 18;
    const usableWidth = pageWidth - margin * 2;
    const usableHeight = pageHeight - margin * 2;
    const imgWidth = usableWidth;
    const imgHeight = (canvas.height * usableWidth) / canvas.width;

    let heightLeft = imgHeight;
    let position = margin;

    pdf.addImage(imgData, "PNG", margin, position, imgWidth, imgHeight, undefined, "FAST");
    heightLeft -= usableHeight;

    while (heightLeft > 2) {
      position = margin - (imgHeight - heightLeft);
      pdf.addPage();
      pdf.addImage(imgData, "PNG", margin, position, imgWidth, imgHeight, undefined, "FAST");
      heightLeft -= usableHeight;
    }

    pdf.save(stampFileName("pdf"));
  }

  function bindDashboardSnapshotExport() {
    const pngBtn = document.getElementById("dashboard-export-png");
    const pdfBtn = document.getElementById("dashboard-export-pdf");
    if (!pngBtn && !pdfBtn) return;

    const run = async (fn, button) => {
      const buttons = [pngBtn, pdfBtn].filter(Boolean);
      buttons.forEach((b) => { b.disabled = true; });
      const original = button?.textContent;
      if (button) button.textContent = "Working…";
      overlay?.classList.remove("d-none");
      try {
        await fn();
      } catch (err) {
        console.error(err);
        window.IceDashboard?.showError?.(err?.message || "Could not export the dashboard snapshot.");
      } finally {
        overlay?.classList.add("d-none");
        buttons.forEach((b) => { b.disabled = false; });
        if (button && original) button.textContent = original;
      }
    };

    pngBtn?.addEventListener("click", () => run(exportDashboardPng, pngBtn));
    pdfBtn?.addEventListener("click", () => run(exportDashboardPdf, pdfBtn));
  }

  function bindExclusionPatternsModal() {
    const modal = document.getElementById("exclusion-filter-modal");
    const listEl = document.getElementById("exclusion-pattern-list");
    const inputEl = document.getElementById("exclusion-pattern-input");
    const addBtn = document.getElementById("exclusion-pattern-add");
    const errorEl = document.getElementById("exclusion-pattern-error");
    if (!modal || !listEl) return;

    const api = "/api/admin/account-exclusions";
    let dirty = false;
    let busy = false;

    function escapeText(value) {
      return String(value ?? "").replace(/[&<>"']/g, (ch) => ({
        "&": "&amp;",
        "<": "&lt;",
        ">": "&gt;",
        '"': "&quot;",
        "'": "&#39;"
      }[ch] || ch));
    }

    function showError(message) {
      if (!errorEl) return;
      errorEl.classList.remove("text-success");
      errorEl.classList.add("text-danger");
      if (!message) {
        errorEl.textContent = "";
        errorEl.classList.add("d-none");
        return;
      }
      errorEl.textContent = message;
      errorEl.classList.remove("d-none");
    }

    function setBusy(next) {
      busy = next;
      if (addBtn) addBtn.disabled = next;
      if (inputEl) inputEl.disabled = next;
      listEl.querySelectorAll("[data-exclusion-id]").forEach((btn) => {
        btn.disabled = next;
      });
    }

    function render(items) {
      if (!items.length) {
        listEl.innerHTML = `<tr><td colspan="3" class="text-secondary">No patterns yet. Add TEST, DEMO, or any account-name fragment.</td></tr>`;
        return;
      }
      listEl.innerHTML = items.map((rule) => {
        const id = rule.id ?? rule.Id;
        const pattern = rule.pattern ?? rule.Pattern ?? "";
        const active = rule.isActive ?? rule.IsActive;
        const status = active
          ? `<span class="badge text-bg-success">Active</span>`
          : `<span class="badge text-bg-secondary">Inactive</span>`;
        return `<tr>
          <td><code>${escapeText(pattern)}</code></td>
          <td>${status}</td>
          <td class="text-end">
            <button type="button" class="btn btn-sm btn-outline-danger" data-exclusion-id="${escapeText(id)}">Delete</button>
          </td>
        </tr>`;
      }).join("");
    }

    async function parseError(res) {
      const body = await res.json().catch(() => null);
      return body?.error || body?.title || `Request failed (${res.status})`;
    }

    async function load() {
      showError("");
      listEl.innerHTML = `<tr><td colspan="3" class="text-secondary">Loading…</td></tr>`;
      try {
        const res = await fetch(`${api}?page=1&pageSize=200`, { headers: { Accept: "application/json" } });
        if (!res.ok) throw new Error(await parseError(res));
        const data = await res.json();
        render(data.items || data.Items || []);
      } catch (err) {
        listEl.innerHTML = `<tr><td colspan="3" class="text-danger">${escapeText(err?.message || "Could not load patterns.")}</td></tr>`;
      }
    }

    async function addPattern() {
      const pattern = (inputEl?.value || "").trim();
      if (!pattern) {
        showError("Enter a pattern, for example TEST.");
        inputEl?.focus();
        return;
      }
      if (busy) return;
      setBusy(true);
      showError("");
      try {
        const res = await fetch(api, {
          method: "POST",
          headers: { "Content-Type": "application/json", Accept: "application/json" },
          body: JSON.stringify({
            pattern,
            matchType: "CONTAINS",
            isActive: true,
            description: `Exclude names containing ${pattern}`,
            createdBy: "DASHBOARD"
          })
        });
        if (!res.ok) throw new Error(await parseError(res));
        if (inputEl) inputEl.value = "";
        dirty = true;
        await load();
        showError("");
        if (errorEl) {
          errorEl.classList.remove("d-none", "text-danger");
          errorEl.classList.add("text-success");
          errorEl.textContent = `Saved “${pattern}”. Close this popup to refresh the dashboard.`;
        }
      } catch (err) {
        showError(err?.message || "Could not add the pattern.");
      } finally {
        setBusy(false);
      }
    }

    async function deletePattern(id) {
      if (busy) return;
      if (!window.confirm("Delete this exclusion pattern?")) return;
      setBusy(true);
      showError("");
      try {
        const res = await fetch(`${api}/${encodeURIComponent(id)}`, { method: "DELETE", headers: { Accept: "application/json" } });
        if (!res.ok && res.status !== 204) throw new Error(await parseError(res));
        dirty = true;
        await load();
      } catch (err) {
        showError(err?.message || "Could not delete the pattern.");
      } finally {
        setBusy(false);
      }
    }

    modal.addEventListener("show.bs.modal", () => {
      dirty = false;
      if (inputEl) inputEl.value = "";
      showError("");
      load();
    });

    modal.addEventListener("hidden.bs.modal", () => {
      if (!dirty) return;
      window.location.reload();
    });

    addBtn?.addEventListener("click", addPattern);
    inputEl?.addEventListener("keydown", (e) => {
      if (e.key === "Enter") {
        e.preventDefault();
        addPattern();
      }
    });
    listEl.addEventListener("click", (e) => {
      const btn = e.target instanceof Element ? e.target.closest("[data-exclusion-id]") : null;
      if (!btn) return;
      deletePattern(btn.getAttribute("data-exclusion-id"));
    });
  }

  bindExclusionPatternsModal();

  window.IceDashboard = {
    showError(message) {
      const host = document.getElementById("alert-host");
      if (!host) return;
      host.textContent = message;
      host.classList.remove("d-none");
    },
    showLoading(show) {
      overlay?.classList.toggle("d-none", !show);
    },
    renderLineChart,
    renderBarChart,
    renderPieChart,
    renderDashboardCharts,
    bindKpiSqlModal,
    bindKpiChartModal,
    bindKpiLinesModal,
    bindDashboardSnapshotExport,
    renderSparklines
  };
})();
