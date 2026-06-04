const C = {
  ink: "#06162F",
  muted: "#5A6B82",
  axis: "#0B3B91",
  grid: "#B9C7D8",
  panel: "#FFFFFF",
  soft: "#F4F8FF",
  blue: "#0B3B91",
  blue2: "#2F80ED",
  green: "#2EAD4B",
  purple: "#8D4BD8",
  orange: "#FF7A1A",
  yellow: "#FFC928",
  teal: "#38B7B9",
  red: "#EF4E63",
};

function line(slide, ctx, x, y, w, h, color = C.grid, width = 1, style = "solid") {
  ctx.addShape(slide, { x, y, w, h, fill: color, line: ctx.line(color, width, style) });
}

function text(slide, ctx, x, y, w, h, value, opt = {}) {
  return ctx.addText(slide, {
    x, y, w, h,
    text: value,
    fontSize: opt.size ?? 20,
    color: opt.color ?? C.ink,
    bold: Boolean(opt.bold),
    align: opt.align ?? "left",
    valign: opt.valign ?? "top",
    typeface: opt.face ?? "Microsoft YaHei UI",
    insets: opt.insets ?? { left: 0, right: 0, top: 0, bottom: 0 },
  });
}

function pill(slide, ctx, x, y, w, h, label, color, iconText = "", labelSize = 19) {
  ctx.addShape(slide, { x, y, w, h, fill: color, line: ctx.line(color, 1) });
  ctx.addShape(slide, { x: x + 10, y: y + 8, w: h - 16, h: h - 16, fill: "#FFFFFF", line: ctx.line("#FFFFFF", 1), geometry: "ellipse" });
  if (iconText) text(slide, ctx, x + 12, y + 13, h - 20, h - 24, iconText, { size: 15, bold: true, color, align: "center", face: "Aptos" });
  text(slide, ctx, x + h + 4, y + 7, w - h - 12, h - 10, label, { size: labelSize, bold: true, color: "#FFFFFF" });
}

function bullets(slide, ctx, x, y, items, color = C.ink, size = 16, gap = 25, width = 260) {
  items.forEach((item, i) => {
    text(slide, ctx, x, y + i * gap, 16, 20, "•", { size, color, bold: true });
    text(slide, ctx, x + 20, y + i * gap, width, 22, item, { size, color });
  });
}

function marker(slide, ctx, x, y, cfg) {
  pill(slide, ctx, x, y, cfg.w, cfg.h, cfg.name, cfg.color, cfg.icon, cfg.labelSize ?? 19);
  bullets(slide, ctx, x + 8, y + cfg.h + (cfg.bulletTop ?? 10), cfg.items, C.ink, cfg.bulletSize ?? 15, cfg.gap ?? 24, cfg.bulletWidth ?? 260);
}

function legendItem(slide, ctx, x, y, color, symbol, title, subtitle) {
  ctx.addShape(slide, { x, y: y + 2, w: 38, h: 38, fill: color, line: ctx.line(color, 1), geometry: "ellipse" });
  text(slide, ctx, x, y + 10, 38, 20, symbol, { size: 15, bold: true, color: "#FFFFFF", align: "center", face: "Aptos" });
  text(slide, ctx, x + 54, y, 126, 24, title, { size: 17, bold: true });
  text(slide, ctx, x + 54, y + 24, 126, 26, subtitle, { size: 12 });
}

export async function slide01(presentation, ctx) {
  const slide = presentation.slides.add();
  slide.background.fill = "#F8FBFF";
  ctx.addShape(slide, { x: 0, y: 0, w: 1280, h: 720, fill: "#F8FBFF", line: ctx.line("#F8FBFF", 0) });

  text(slide, ctx, 54, 32, 800, 58, "Agent Framework Landscape (2026)", { size: 42, bold: true, face: "Aptos Display" });
  text(slide, ctx, 56, 98, 640, 34, "主流 Agent 框架能力与开发效率对比", { size: 24, bold: true, color: C.muted });

  const ox = 168;
  const oy = 590;
  const gw = 840;
  const gh = 435;
  const top = oy - gh;

  for (let i = 1; i <= 4; i++) {
    const x = ox + (gw / 4) * i;
    line(slide, ctx, x, top, 1, gh, C.grid, 1, "dash");
  }
  for (let i = 1; i <= 4; i++) {
    const y = top + (gh / 4) * i;
    line(slide, ctx, ox, y, gw, 1, C.grid, 1, "dash");
  }

  line(slide, ctx, ox, top - 12, 1, gh + 12, C.axis, 3);
  line(slide, ctx, ox, oy, gw + 16, 1, C.axis, 3);
  ctx.addShape(slide, { geometry: "triangle", x: ox - 9, y: top - 34, w: 18, h: 22, fill: C.axis, line: ctx.line(C.axis, 1) });
  ctx.addShape(slide, { geometry: "triangle", x: ox + gw + 8, y: oy - 9, w: 22, h: 18, fill: C.axis, line: ctx.line(C.axis, 1) });

  text(slide, ctx, 130, top - 8, 38, 30, "高", { size: 22, bold: true });
  text(slide, ctx, 130, oy - 8, 38, 30, "低", { size: 22, bold: true });
  text(slide, ctx, ox - 140, 360, 120, 48, "企业级能力", { size: 25, bold: true, color: C.axis });
  text(slide, ctx, ox - 128, 414, 110, 58, "(Enterprise\nCapability)", { size: 18, color: C.axis });
  text(slide, ctx, 548, 610, 230, 26, "开发效率", { size: 27, bold: true, color: C.axis, align: "center" });
  text(slide, ctx, 535, 642, 260, 20, "(Developer Efficiency)", { size: 17, color: C.axis, align: "center", face: "Aptos" });
  text(slide, ctx, ox - 15, 618, 70, 68, "低\n学习成本低\n(易上手)", { size: 18, bold: true, color: C.axis });
  text(slide, ctx, ox + gw - 90, 610, 100, 68, "高\n学习成本高\n(更灵活)", { size: 18, bold: true, color: C.axis, align: "right" });

  marker(slide, ctx, 190, 455, {
    name: "CrewAI", color: C.purple, icon: "C", w: 126, h: 38,
    items: ["角色分工明确", "上手简单", "快速构建团队式 Agent"],
  });
  marker(slide, ctx, 335, 382, {
    name: "AutoGen", color: C.orange, icon: "A", w: 126, h: 38,
    items: ["多 Agent 协作", "Agent 对话机制", "研究与实验友好"],
  });
  marker(slide, ctx, 474, 220, {
    name: "Semantic\nKernel", color: C.blue2, icon: "SK", w: 188, h: 58,
    items: ["微软生态深度集成", "Plugin & Memory", "企业应用友好"],
    gap: 24, bulletWidth: 210,
  });
  marker(slide, ctx, 620, 442, {
    name: "OpenAI\nAgents SDK", color: C.yellow, icon: "AI", w: 184, h: 62,
    items: ["官方 SDK", "集成简单", "与 OpenAI 模型最佳配合"],
    bulletSize: 14, bulletTop: 16, labelSize: 17, bulletWidth: 220,
  });
  marker(slide, ctx, 735, 328, {
    name: "LangGraph", color: C.green, icon: "LG", w: 168, h: 38,
    items: ["基于状态机的工作流", "灵活强大", "社区活跃"],
    bulletWidth: 210,
  });
  marker(slide, ctx, 792, 132, {
    name: "Microsoft\nAgent FW", color: C.blue, icon: "MAF", w: 206, h: 62,
    items: ["Workflow 原生支持", "State & Checkpoint", "Observability", "Human-in-the-loop", "企业级能力最强"],
    gap: 24, labelSize: 18, bulletTop: 8, bulletSize: 13, bulletWidth: 190,
  });

  ctx.addShape(slide, { x: 1040, y: 160, w: 220, h: 500, fill: "#FFFFFF", line: ctx.line("#D3DCE8", 1) });
  text(slide, ctx, 1062, 182, 176, 30, "关键能力对比维度", { size: 20, bold: true, color: C.axis, align: "center" });
  const legends = [
    [C.blue2, "WF", "工作流能力", "(Workflow)"],
    [C.green, "MA", "多 Agent 协作", "(Multi-Agent)"],
    [C.purple, "ST", "状态管理", "(State Mgmt.)"],
    [C.yellow, "ME", "记忆能力", "(Memory)"],
    [C.blue2, "OB", "可观测性", "(Observability)"],
    [C.red, "HI", "人工干预", "(Human-in-the-loop)"],
    [C.teal, "ER", "企业级特性", "(Enterprise Ready)"],
  ];
  legends.forEach((l, i) => legendItem(slide, ctx, 1064, 232 + i * 58, l[0], l[1], l[2], l[3]));

  ctx.addShape(slide, { x: 284, y: 668, w: 620, h: 42, fill: "#EFF6FF", line: ctx.line("#A9C6EA", 1) });
  ctx.addShape(slide, { geometry: "ellipse", x: 308, y: 679, w: 20, h: 20, fill: C.axis, line: ctx.line(C.axis, 1) });
  text(slide, ctx, 309, 680, 18, 18, "★", { size: 12, bold: true, color: "#FFFFFF", align: "center" });
  text(slide, ctx, 342, 674, 520, 28, "Microsoft Agent Framework 在企业级能力与开发效率上达到最佳平衡，适合构建生产级 Agent 应用。", { size: 14, bold: true, color: C.axis });

  return slide;
}
