using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Kartaclysm {
public partial class MainForm {
  const float X0 = SIDE + 44, XR = W - 44;   // content column

  void DrawPage(Graphics g, Page p) {
    switch (p) {
      case Page.Home: DrawHome(g); break;
      case Page.Tracks: DrawTracks(g); break;
      case Page.Updates: DrawUpdates(g); break;
      case Page.Online: DrawOnline(g); break;
      case Page.Settings: DrawSettings(g); break;
    }
  }
  void Header(Graphics g, string kicker, string title, string sub) {
    using (var b = new SolidBrush(Cyan)) g.FillRectangle(b, X0, 62, 22, 3);
    Spaced(g, kicker, F(fBold, 12), Cyan, X0 + 32, 56, 3);
    Spaced(g, title, F(fBold, 40), TextC, X0 - 2, 74, 2);
    if (sub != null) Txt(g, sub, F(fMed, 15), Muted, X0, 124);
  }

  // ======================= HOME =======================
  static readonly (string date, string tag, string title, string body)[] News = {
    ("OCT 20, 2026", "LAUNCHER", "Kartaclysm launcher 1.4", "Brand-new look: track browser, online status, settings and a cinematic intro."),
    ("OCT 20, 2026", "PACK", "136 retro tracks lined up", "34 cups of retro ports by the Custom Mario Kart community."),
    ("COMING SOON", "ONLINE", "Wiimmfi registration", "Online races for the full pack open as soon as the pack is registered."),
  };
  void DrawHome(Graphics g) {
    float t = Now;
    Spaced(g, "SEASON 01  ·  RETRO TRACK PACK", F(fBold, 12), Cyan, X0 + 2, 96, 3.2f);
    DrawA(g, wordmark, new RectangleF(X0 - 10, 116, 560, 560f * wordmark.Height / wordmark.Width), 1);
    float y = 116 + 560f * wordmark.Height / wordmark.Width + 22;
    foreach (var line in Wrap(g, "Every classic course from SNES to 3DS, rebuilt as faithful retro ports. One click to update, one click to race.", F(fMed, 17), 500)) { Txt(g, line, F(fMed, 17), Color.FromArgb(200, 210, 230), X0, y); y += 25; }
    y += 14;
    float cx = X0;
    Chip(g, cx, y, "136 TRACKS", Cyan, out var w1); cx += w1 + 10;
    Chip(g, cx, y, "34 CUPS", Magenta, out var w2); cx += w2 + 10;
    Chip(g, cx, y, s.Mode == "sd" ? "REAL WII · USB" : s.Mode == "native" ? "NATIVE · WIICOMPILED" : "DOLPHIN", Ok, out _);
    y += 58;
    bool sd = s.Mode == "sd";
    PrimaryButton(g, new RectangleF(X0, y, 280, 70), "play", busy ? "WORKING" : sd ? "EXPORT" : "PLAY", sd ? "sd" : "play", () => _ = OnPlay(), !busy);
    GhostButton(g, new RectangleF(X0 + 296, y, 190, 70), "tracksbtn", "TRACKS", "grid", () => Go(Page.Tracks), 17);
    y += 98;
    // status
    Txt(g, status.ToUpperInvariant(), F(fBold, 14), TextC, X0, y);
    ProgressBar(g, new RectangleF(X0, y + 26, 486, 5), showBar ? (float)progress : 0);
    var pv = string.IsNullOrEmpty(s.PackVersion) ? "NOT INSTALLED" : "V" + s.PackVersion;
    Spaced(g, $"PACK {pv}   ·   LAUNCHER V{LauncherVersion}   ·   {(sd ? "MODE: REAL WII" : s.Mode == "native" ? "MODE: NATIVE" : "MODE: DOLPHIN")}", F(fSemi, 11), Dim, X0, y + 42, 1.4f);

    // news column
    float nx = W - 44 - 300, ny = 92;
    Spaced(g, "LATEST NEWS", F(fBold, 12), Muted, nx, ny, 3); ny += 26;
    for (int i = 0; i < News.Length; i++) {
      var n = News[i]; var r = new RectangleF(nx, ny, 300, 116); string id = "news" + i; bool hv = Hover(id);
      Glass(g, r, 14, hv ? 120 : 150);
      using (var b = new SolidBrush(i == 0 ? Cyan : i == 1 ? Magenta : Warn)) g.FillRectangle(b, r.X, r.Y + 18, 3, 22);
      Spaced(g, n.tag, F(fBold, 11), i == 0 ? Cyan : i == 1 ? Magenta : Warn, r.X + 18, r.Y + 16, 2.4f);
      var dw = Measure(g, n.date, F(fSemi, 11)).Width; Txt(g, n.date, F(fSemi, 11), Dim, r.Right - 16 - dw, r.Y + 16);
      Txt(g, n.title, F(fBold, 17), TextC, r.X + 18, r.Y + 34);
      float ly = r.Y + 60; foreach (var l in Wrap(g, n.body, F(fMed, 13), 264).Take(2)) { Txt(g, l, F(fMed, 13), Muted, r.X + 18, ly); ly += 19; }
      AddHit(r, id, () => Go(Page.Updates));
      ny += 128;
    }
    // now showing + slideshow dots
    var cap = shotCaption[shot].Split('|');
    float by = H - 64;
    Spaced(g, "NOW SHOWING", F(fBold, 10), Dim, X0, by, 3);
    Txt(g, cap[0], F(fBold, 15), TextC, X0, by + 16);
    if (cap.Length > 1 && cap[1].Length > 0) { var cw = Measure(g, cap[0], F(fBold, 15)).Width; Spaced(g, "·  " + cap[1], F(fSemi, 12), Muted, X0 + cw + 10, by + 19, 1.2f); }
    float dx = XR - shots.Count * 26;
    for (int i = 0; i < shots.Count; i++) {
      var r = new RectangleF(dx + i * 26, by + 18, 20, 12); string id = "dot" + i; int ii = i;
      bool act = i == shot;
      float fill = act ? Clamp01((Now - shotAt) / SHOT_LEN) : 0;
      using (var gp = Round(new RectangleF(r.X, r.Y + 4, 20, 4), 2)) using (var b = new SolidBrush(Color.FromArgb(Hover(id) ? 120 : 60, 255, 255, 255))) g.FillPath(b, gp);
      if (act) using (var gp = Round(new RectangleF(r.X, r.Y + 4, Math.Max(4, 20 * fill), 4), 2)) using (var b = new SolidBrush(Cyan)) g.FillPath(b, gp);
      AddHit(r, id, () => GoShot(ii));
    }
  }

  // ======================= TRACKS =======================
  static readonly string[] Filters = { "ALL", "SNES", "N64", "GBA", "GCN", "DS", "WII", "3DS", "TOUR", "SW", "RMX" };
  IEnumerable<CupInfo> FilteredCups() => trackFilter == "ALL" ? data.cups : data.cups.Where(c => c.series.ToUpperInvariant().Split('/').Any(x => x.StartsWith(trackFilter)) || (trackFilter == "SW" && c.series.ToUpperInvariant().Contains("SW")));
  const float GRID_TOP = 196, CARD_H = 226, CARD_GAP = 18;
  float TracksMaxScroll() { int rows = (FilteredCups().Count() + 2) / 3; return Math.Max(0, rows * (CARD_H + CARD_GAP) - (H - GRID_TOP - 24)); }
  static Color SeriesColor(string s) {
    s = s.ToUpperInvariant();
    if (s.StartsWith("SNES")) return Color.FromArgb(150, 110, 255);
    if (s.StartsWith("N64")) return Color.FromArgb(255, 80, 90);
    if (s.StartsWith("GBA")) return Color.FromArgb(70, 140, 255);
    if (s.StartsWith("GCN")) return Color.FromArgb(120, 90, 230);
    if (s.StartsWith("DS")) return Color.FromArgb(200, 200, 215);
    if (s.StartsWith("3DS")) return Color.FromArgb(255, 70, 70);
    if (s.StartsWith("WII")) return Color.FromArgb(80, 200, 255);
    if (s.StartsWith("TOUR")) return Color.FromArgb(255, 170, 40);
    if (s.StartsWith("SW")) return Color.FromArgb(255, 60, 60);
    return Color.FromArgb(0, 229, 200);
  }
  void DrawTracks(Graphics g) {
    int nTracks = data.cups.Sum(c => c.tracks.Count);
    Header(g, "TRACK LIST", "TRACKS", $"{nTracks} tracks in {data.cups.Count} cups · faithful retro ports, credited to their authors");
    float fx = X0; 
    foreach (var f in Filters) {
      var fnt = F(fBold, 12); float lw = Spaced(g, f, fnt, TextC, 0, 0, 1.6f, false); var r = new RectangleF(fx, 152, lw + 28, 28); string id = "flt" + f; bool act = trackFilter == f, hv = Hover(id);
      using (var gp = Round(r, 14)) {
        if (act) using (var lg = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y, r.Width + 2, r.Height), Cyan, Blue, 0f)) g.FillPath(lg, gp);
        else { Glass(g, r, 14, hv ? 110 : 150); using var pen = new Pen(Color.FromArgb(hv ? 120 : 40, 255, 255, 255)); g.DrawPath(pen, gp); }
      }
      Spaced(g, f, fnt, act ? Color.FromArgb(4, 14, 30) : TextC, r.X + 14, r.Y + 7, 1.6f);
      string ff = f; AddHit(r, id, () => { trackFilter = ff; tracksScroll = tracksScrollTarget = 0; });
      fx += r.Width + 8;
    }
    var cups = FilteredCups().ToList();
    float cw = (XR - X0 - 2 * CARD_GAP) / 3;
    var clip = new RectangleF(X0 - 20, GRID_TOP - 6, XR - X0 + 40, H - GRID_TOP + 6);
    var st = g.Save(); g.SetClip(clip);
    for (int i = 0; i < cups.Count; i++) {
      var c = cups[i]; float x = X0 + (i % 3) * (cw + CARD_GAP), y = GRID_TOP + (i / 3) * (CARD_H + CARD_GAP) - tracksScroll;
      if (y > H || y + CARD_H < GRID_TOP - 10) continue;
      var r = new RectangleF(x, y, cw, CARD_H); string id = "cup" + c.n; bool hv = Hover(id);
      Glass(g, r, 14, hv ? 125 : 155);
      var sc = SeriesColor(c.series);
      // banner
      var ban = new RectangleF(x, y, cw, 74);
      using (var gp = Round(ban, 14)) {
        var s2 = g.Save(); g.SetClip(gp, CombineMode.Intersect); g.SetClip(new RectangleF(x, y, cw, 74), CombineMode.Intersect);
        var withShot = c.tracks.FirstOrDefault(tk => tk.shot.HasValue);
        if (withShot != null) {
          int si = withShot.shot.Value + (shots.Count - data.shots.Count);
          var img = shots[si]; float ar = cw / 74f; float sh = img.Height * 0.55f, sw = sh * ar;
          DrawA(g, img, ban, 0.9f, new RectangleF((img.Width - sw) / 2, img.Height * 0.25f, sw, sh));
        } else {
          using var lg = new LinearGradientBrush(new RectangleF(ban.X - 1, ban.Y, ban.Width + 2, ban.Height), A(sc, 150), A(sc, 30), 20f); g.FillRectangle(lg, ban);
          using var pen = new Pen(Color.FromArgb(22, 255, 255, 255), 10f);
          for (float k = -80; k < cw + 80; k += 34) g.DrawLine(pen, x + k, y + 80, x + k + 60, y - 6);
        }
        using (var lg = new LinearGradientBrush(new RectangleF(ban.X, ban.Y - 1, ban.Width, ban.Height + 2), Color.FromArgb(10, 8, 12, 22), Color.FromArgb(200, 8, 12, 22), 90f)) g.FillRectangle(lg, ban);
        g.Restore(s2);
      }
      Txt(g, $"{c.n:00}", F(fBold, 26), Color.FromArgb(80, 255, 255, 255), x + cw - 44, y + 10);
            Txt(g, (c.name ?? $"CUP {c.n:00}").ToUpperInvariant(), F(fBold, 22), TextC, x + 16, y + 36);
      // series chip
      var sf = F(fBold, 10); float sw2 = Spaced(g, c.series.ToUpperInvariant(), sf, TextC, 0, 0, 1.4f, false);
      var chip = new RectangleF(x + 16, y + 12, sw2 + 16, 18);
      using (var gp = Round(chip, 9)) { using var b = new SolidBrush(A(sc, 60)); g.FillPath(b, gp); using var pen = new Pen(A(sc, 200)); g.DrawPath(pen, gp); }
      Spaced(g, c.series.ToUpperInvariant(), sf, TextC, chip.X + 8, chip.Y + 3, 1.4f);
      float ty = y + 86;
      for (int k = 0; k < c.tracks.Count && k < 4; k++) {
        var tk = c.tracks[k];
        using (var b = new SolidBrush(A(sc, 230))) g.FillRectangle(b, x + 16, ty + 5, 3, 26);
        Txt(g, Ellipsis(g, tk.name, F(fBold, 14), cw - 44), F(fBold, 14), TextC, x + 28, ty + 2);
        Txt(g, Ellipsis(g, "by " + tk.authors, F(fMed, 12), cw - 44), F(fMed, 12), Muted, x + 28, ty + 19);
        ty += 34;
      }
      if (hv) using (var gp = Round(r, 14)) using (var pen = new Pen(A(Cyan, 160), 1.3f)) g.DrawPath(pen, gp);
      hits.Add(new Hit { R = RectangleF.Intersect(r, clip), Id = id, Click = null });
    }
    g.Restore(st);
    // fade top/bottom of scrolling area
    // scrollbar
    float max = TracksMaxScroll();
    if (max > 0) {
      float trackH = H - GRID_TOP - 24, thumb = Math.Max(40, trackH * trackH / (trackH + max)), ty2 = GRID_TOP + (trackH - thumb) * (tracksScroll / max);
      using (var gp = Round(new RectangleF(W - 22, GRID_TOP, 4, trackH), 2)) using (var b = new SolidBrush(Color.FromArgb(30, 255, 255, 255))) g.FillPath(b, gp);
      using (var gp = Round(new RectangleF(W - 22, ty2, 4, thumb), 2)) using (var b = new SolidBrush(A(Cyan, 200))) g.FillPath(b, gp);
    }
  }

  // ======================= UPDATES =======================
  static readonly (string ver, string date, string kind, string[] items)[] Changelog = {
    ("LAUNCHER 1.11.0", "OCT 21, 2026", "MAJOR", new[] { "Ready for Kartaclysm's own online server: Worldwide and friend rooms for every region (PAL, USA, Japan, Korea), Dolphin and real Wii", "Online page checks the Kartaclysm servers when the pack uses them", "No Wii certificate setup needed for the Kartaclysm server", "Fixed: the home page said 33 cups" }),
    ("LAUNCHER 1.10.0", "OCT 21, 2026", "UPDATE", new[] { "New launcher music: Stalemate by KzX (Kayoszx), from Newgrounds, used with the author's permission", "Music on/off toggle still in Settings", "Music credit added to About & Credits" }),
    ("LAUNCHER 1.9.0", "OCT 21, 2026", "UPDATE", new[] { "Online setup check: tells you if Dolphin is missing the Wii network certificates Wiimmfi needs", "One-click guided fix in English and Italian: Kartaclysm opens Dolphin with your game listed, then right-click it → Perform System Update (from your own disc)", "Note about Wiimmfi error 23899 (new console, wait 7 days) on the first connection", "Custom cups in Time Trials, readable cup icons, fixed title logo (packs 1.2.3-1.2.6)" }),
    ("LAUNCHER 1.8.0", "OCT 20, 2026", "MAJOR", new[] { "Original Kartaclysm theme music in the launcher (mute it in Settings)", "Kartaclysm title screen in the game", "NATIVE PC mode is fully automatic: PLAY downloads WiiCompiled, builds Kartaclysm from your own game and starts it", "Progress bar during the native build, errors saved to native-error.log", "Native mode uses its own portable settings, so a Retro Rewind WiiCompiled install isn't touched" }),
    ("LAUNCHER 1.6.0 + PACK 1.1.0", "OCT 20, 2026", "MAJOR", new[] { "Fixed: PLAY could start plain Mario Kart Wii (old launchers used a Riivolution shortcut that can't run LE-CODE)", "PLAY now always builds/verifies your Kartaclysm game file first, with progress, and shows any error", "Launcher updates itself automatically", "New NATIVE PC play mode for your WiiCompiled Kartaclysm build", "New cup icons with a symbol for every disaster", "New 34th cup (Eruption) - fixes the empty slot/background on the last cup page" }),
    ("PACK 1.0.0", "OCT 20, 2026", "PACK", new[] { "The real Kartaclysm pack: 132 custom tracks in 33 named cups + the 8 original cups (LE-CODE)", "First PLAY builds the game files from YOUR copy on your PC (a few minutes, only once)", "Wiimmfi-ready: own Kartaclysm region as soon as Wiimm assigns it" }),
    ("LAUNCHER 1.5.0", "OCT 20, 2026", "UPDATE", new[] { "Fits smaller screens (e.g. laptops at 125% scaling)", "Smoother Dolphin: background shader compiling", "Online page checks the real Wiimmfi game servers", "PLAY builds and starts your own Kartaclysm game file (RMCPKC, your normal save is untouched)", "Real Wii: copies the game file to USB/SD for a USB loader" }),
    ("LAUNCHER 1.4.1", "OCT 20, 2026", "UPDATE", new[] { "All 33 cups now have names" }),
    ("LAUNCHER 1.4.0", "OCT 20, 2026", "MAJOR", new[] { "All-new interface: sidebar, Home, Tracks, Updates, Online and Settings pages", "Track browser with all 33 cups and every track author credited", "Online page: live server status and tips for players in Italy", "Cinematic intro (skip with a click, turn off in Settings)", "Play mode: Dolphin on PC or real Wii via SD card", "Fixed: 1.3.0 could crash at startup" }),
    ("PACK 0.2.0", "OCT 20, 2026", "PACK", new[] { "Pack renamed to Kartaclysm (Riivolution XML + folder)", "Placeholder content while the 132-track pack is being built" }),
    ("LAUNCHER 1.1 – 1.3", "OCT 20, 2026", "UPDATE", new[] { "Auto-update: only changed files are downloaded", "Export to SD card for real Wii consoles" }),
    ("LAUNCHER 1.0.0", "OCT 20, 2026", "RELEASE", new[] { "First release" }),
  };
  void DrawUpdates(Graphics g) {
    Header(g, "CHANGELOG", "UPDATES", "Launcher and pack updates install automatically when you press PLAY.");
    float y = 172, lx = X0 + 10;
    using (var lg = new LinearGradientBrush(new RectangleF(lx, y, 2, H - y - 30), A(Cyan, 200), A(Magenta, 0), 90f)) g.FillRectangle(lg, lx, y + 10, 2, H - y - 40);
    for (int i = 0; i < Changelog.Length; i++) {
      var e = Changelog[i]; int n = e.items.Length; float h = 66 + n * 24;
      if (y + h > H - 20) break;
      var col = i == 0 ? Cyan : e.kind == "PACK" ? Magenta : Muted;
      using (var b = new SolidBrush(A(col, 60))) g.FillEllipse(b, lx - 8, y + 16, 18, 18);
      using (var b = new SolidBrush(col)) g.FillEllipse(b, lx - 4, y + 20, 10, 10);
      var r = new RectangleF(lx + 30, y, XR - lx - 30, h);
      Glass(g, r, 14, 150);
      Txt(g, e.ver, F(fBold, 20), TextC, r.X + 22, r.Y + 16);
      float vw = Measure(g, e.ver, F(fBold, 20)).Width;
      var kf = F(fBold, 10); float kw = Spaced(g, e.kind, kf, TextC, 0, 0, 1.6f, false);
      var kr = new RectangleF(r.X + 34 + vw, r.Y + 20, kw + 16, 18);
      using (var gp = Round(kr, 9)) { using var b = new SolidBrush(A(col, 50)); g.FillPath(b, gp); using var pen = new Pen(A(col, 200)); g.DrawPath(pen, gp); }
      Spaced(g, e.kind, kf, TextC, kr.X + 8, kr.Y + 3, 1.6f);
      var dw = Measure(g, e.date, F(fSemi, 12)).Width; Txt(g, e.date, F(fSemi, 12), Dim, r.Right - 22 - dw, r.Y + 22);
      float iy = r.Y + 54;
      foreach (var it in e.items) { using (var b = new SolidBrush(A(col, 220))) g.FillRectangle(b, r.X + 24, iy + 8, 6, 2); Txt(g, it, F(fMed, 14), Color.FromArgb(205, 214, 232), r.X + 40, iy); iy += 24; }
      y += h + 16;
    }
  }

  // ======================= ONLINE =======================
  void DrawOnline(Graphics g) {
    bool own = OwnServer;
    Header(g, "NETWORK", "ONLINE", own ? "Live check of the Kartaclysm game servers from your PC." : "Live check of the Wiimmfi game servers from your PC.");
    GhostButton(g, new RectangleF(XR - 150, 70, 150, 40), "recheck", checking ? "CHECKING" : "RE-CHECK", "refresh", () => _ = CheckOnline(), 13);
    float cw = (XR - X0 - 2 * 18) / 3, y = 172;
    for (int i = 0; i < services.Count; i++) {
      var sv = services[i]; var r = new RectangleF(X0 + i * (cw + 18), y, cw, 132);
      Glass(g, r, 14, 150);
      var c = sv.State == 1 ? Ok : sv.State == 2 ? Warn : sv.State == 3 ? Bad : Dim;
      float pulse = 0.5f + 0.5f * MathF.Sin(Now * 3 + i);
      using (var b = new SolidBrush(A(c, (int)(40 + 50 * pulse)))) g.FillEllipse(b, r.X + 16, r.Y + 18, 18, 18);
      using (var b = new SolidBrush(c)) g.FillEllipse(b, r.X + 21, r.Y + 23, 8, 8);
      Spaced(g, sv.Name, F(fBold, 15), TextC, r.X + 44, r.Y + 18, 2);
      Txt(g, sv.State == 1 ? "OPERATIONAL" : sv.State == 2 ? "DEGRADED" : sv.State == 3 ? "UNREACHABLE" : "CHECKING…", F(fBold, 22), c, r.X + 18, r.Y + 50);
      Txt(g, Ellipsis(g, sv.Detail, F(fMed, 13), cw - 36), F(fMed, 13), Muted, r.X + 18, r.Y + 82);
      Txt(g, Ellipsis(g, sv.Desc, F(fMed, 12), cw - 36), F(fMed, 12), Dim, r.X + 18, r.Y + 104);
    }
    y += 150;
    var ir = new RectangleF(X0, y, (XR - X0) * 0.62f, H - y - 36);
    Glass(g, ir, 14, 150);
    Spaced(g, "PLAYING FROM ITALY?", F(fBold, 16), TextC, ir.X + 22, ir.Y + 20, 2);
    Txt(g, "If online won't connect, try these in order:", F(fMed, 14), Muted, ir.X + 22, ir.Y + 48);
    string[] tips = {
      own ? "Turn off any VPN, proxy or iCloud Private Relay." : "Turn off any VPN, proxy or iCloud Private Relay — the servers block them.",
      "Stuck on “Connecting…”? Set your DNS to Cloudflare 1.1.1.1 or Google 8.8.8.8.",
      own ? "Use a clean game file (Kartaclysm patches it for its server itself) and turn off RetroAchievements in Dolphin."
          : "Use a clean game file (Kartaclysm patches it for Wiimmfi itself) and turn off RetroAchievements in Dolphin.",
      "Real Wii: use 2.4 GHz Wi-Fi with WPA2 (AES), channel 11 or lower.",
      own ? "Still blocked? Report it on the Kartaclysm GitHub (Issues) with a screenshot of this page."
          : "Still blocked? Ask on the Wiimmfi Discord / forum — the team can check your connection.",
    };
    float ty = ir.Y + 80;
    for (int i = 0; i < tips.Length; i++) {
      var nr = new RectangleF(ir.X + 22, ty, 24, 24);
      using (var gp = Round(nr, 7)) { using var b = new SolidBrush(A(Cyan, 40)); g.FillPath(b, gp); using var pen = new Pen(A(Cyan, 160)); g.DrawPath(pen, gp); }
      var ns = (i + 1).ToString(); var nw = Measure(g, ns, F(fBold, 13)).Width; Txt(g, ns, F(fBold, 13), Cyan, nr.X + (24 - nw) / 2, nr.Y + 4);
      float ly = ty + 2;
      foreach (var l in Wrap(g, tips[i], F(fMed, 14), ir.Width - 90)) { Txt(g, l, F(fMed, 14), Color.FromArgb(210, 218, 235), ir.X + 58, ly); ly += 20; }
      ty = Math.Max(ty + 34, ly + 12);
    }
    var nr2 = new RectangleF(ir.Right + 18, y, XR - ir.Right - 18, ir.Height);
    Glass(g, nr2, 14, 150);
    Spaced(g, "KARTACLYSM ROOMS", F(fBold, 14), TextC, nr2.X + 22, nr2.Y + 20, 2);
    float yy = nr2.Y + 50; bool reg = WiimmfiRegion > 0;
    var txt = own ? $"Kartaclysm races on its own server ({WfcDomain}): Worldwide and friend rooms with only Kartaclysm players, on the same 136 tracks, from every region."
            : reg ? $"Kartaclysm races on Wiimmfi in its own region ({WiimmfiRegion}): you only meet other Kartaclysm players, on the same 136 tracks."
                  : "Kartaclysm will race on Wiimmfi in its own private region, so you only meet other Kartaclysm players. Online opens as soon as Wiimm assigns the region; until then play Grand Prix, VS and Time Trials offline.";
    foreach (var l in Wrap(g, txt, F(fMed, 14), nr2.Width - 44)) { Txt(g, l, F(fMed, 14), Muted, nr2.X + 22, yy); yy += 21; }
    yy += 14;
    Chip(g, nr2.X + 22, yy, own ? "OWN SERVER · ONLINE" : reg ? $"REGION {WiimmfiRegion} · ONLINE" : "REGION PENDING", own || reg ? Ok : Warn, out _);
    yy += 40;
    if (s.Mode == "dolphin" && own) {
      Spaced(g, "DOLPHIN ONLINE SETUP", F(fBold, 13), Ok, nr2.X + 22, yy, 2); yy += 26;
      foreach (var l in Wrap(g, "Nothing to set up: the Kartaclysm server needs no Wii certificates. / Niente da configurare: il server Kartaclysm non richiede certificati Wii.", F(fMed, 13), nr2.Width - 44)) { Txt(g, l, F(fMed, 13), Muted, nr2.X + 22, yy); yy += 19; }
    } else if (s.Mode == "dolphin") {
      bool certs = HasWiiCerts();
      Spaced(g, "DOLPHIN ONLINE SETUP", F(fBold, 13), certs ? Ok : Warn, nr2.X + 22, yy, 2); yy += 26;
      var st = certs ? "Wii certificates found. First connection: error 23899 (wait 7 days) is normal. / Certificati trovati. Primo accesso: l'errore 23899 (attendi 7 giorni) è normale."
                     : "Missing Wii certificates. In Dolphin: right-click Mario Kart Wii → Perform System Update (Avvia l'aggiornamento di sistema).";
      foreach (var l in Wrap(g, st, F(fMed, 13), nr2.Width - 44)) { Txt(g, l, F(fMed, 13), Muted, nr2.X + 22, yy); yy += 19; }
      if (!certs && yy + 44 < nr2.Bottom) GhostButton(g, new RectangleF(nr2.X + 22, yy + 8, Math.Min(260, nr2.Width - 44), 36), "certsetup", "SETUP / CONFIGURA", "online", () => ShowCertGuide(false), 13);
    }
  }

  // ======================= SETTINGS =======================
  void DrawSettings(Graphics g) {
    Header(g, "PREFERENCES", "SETTINGS", null);
    float y = 140, full = XR - X0;
    // --- game files ---
    var r1 = new RectangleF(X0, y, full, 160); Glass(g, r1, 14, 150);
    Spaced(g, "GAME FILES", F(fBold, 13), Cyan, r1.X + 24, r1.Y + 20, 2.6f);
    void Row(float ry, string label, string val, string id, Action a, string hint) {
      Txt(g, label, F(fBold, 15), TextC, r1.X + 24, ry);
      var vr = new RectangleF(r1.X + 200, ry - 7, full - 200 - 24 - 130, 34);
      using (var gp = Round(vr, 8)) { using var b = new SolidBrush(Color.FromArgb(90, 0, 0, 0)); g.FillPath(b, gp); using var pen = new Pen(Color.FromArgb(30, 255, 255, 255)); g.DrawPath(pen, gp); }
      bool set = !string.IsNullOrEmpty(val);
      Txt(g, Ellipsis(g, set ? val : hint, F(fMed, 13), vr.Width - 40), F(fMed, 13), set ? Color.FromArgb(205, 214, 232) : Dim, vr.X + 34, vr.Y + 9);
      using (var b = new SolidBrush(set ? Ok : Warn)) g.FillEllipse(b, vr.X + 14, vr.Y + 13, 8, 8);
      GhostButton(g, new RectangleF(r1.Right - 24 - 116, ry - 7, 116, 34), id, "CHANGE", null, a, 12);
    }
    Row(r1.Y + 62, "Mario Kart Wii", s.GamePath, "chgame", () => AskGame(), "Your own game file (.iso / .wbfs / .rvz)");
    Row(r1.Y + 110, "Dolphin", s.DolphinPath, "chdol", () => AskDolphin(), "Dolphin.exe — free at dolphin-emu.org");
    y += 176;
    // --- play mode ---
    var r2 = new RectangleF(X0, y, full * 0.56f, 190); Glass(g, r2, 14, 150);
    Spaced(g, "PLAY MODE", F(fBold, 13), Cyan, r2.X + 24, r2.Y + 20, 2.6f);
    var seg = new RectangleF(r2.X + 24, r2.Y + 52, r2.Width - 48, 46);
    using (var gp = Round(seg, 12)) { using var b = new SolidBrush(Color.FromArgb(110, 0, 0, 0)); g.FillPath(b, gp); }
    string[] modes = { "dolphin", "native", "sd" }; string[] ml = { "DOLPHIN", "NATIVE PC", "REAL WII" }; string[] mi = { "play", "play", "sd" };
    for (int i = 0; i < 3; i++) {
      var sr = new RectangleF(seg.X + 4 + i * (seg.Width - 8) / 3, seg.Y + 4, (seg.Width - 8) / 3, seg.Height - 8); string id = "mode" + i; int ii = i;
      bool act = s.Mode == modes[i], hv = Hover(id);
      if (act) using (var gp = Round(sr, 9)) using (var lg = new LinearGradientBrush(new RectangleF(sr.X - 1, sr.Y, sr.Width + 2, sr.Height), Cyan, Blue, 0f)) g.FillPath(lg, gp);
      else if (hv) using (var gp = Round(sr, 9)) using (var b = new SolidBrush(Color.FromArgb(24, 255, 255, 255))) g.FillPath(b, gp);
      var f = F(fBold, 13); float lw = Spaced(g, ml[i], f, TextC, 0, 0, 1.6f, false);
      var tc = act ? Color.FromArgb(4, 14, 30) : TextC;
      Icon_(g, mi[i], sr.X + (sr.Width - lw - 26) / 2, sr.Y + 9, tc, 1.6f);
      Spaced(g, ml[i], f, tc, sr.X + (sr.Width - lw - 26) / 2 + 26, sr.Y + 11, 1.6f);
      AddHit(sr, id, () => { s.Mode = modes[ii]; s.Save(); });
    }
    var desc = s.Mode == "native" ? "PLAY builds Kartaclysm natively for your PC with WiiCompiled (no emulator) from your own game, then starts it. First time: ~1 hour and ~20 GB free space." : s.Mode == "sd" ? "PLAY builds Kartaclysm and copies the game file to your USB drive / SD card for a USB loader (USB Loader GX, WiiFlow)." : "PLAY updates the pack, builds Kartaclysm from your game the first time (a few minutes), then starts it in Dolphin.";
    float dy = seg.Bottom + 16; foreach (var l in Wrap(g, desc, F(fMed, 13), r2.Width - 48)) { Txt(g, l, F(fMed, 13), Muted, r2.X + 24, dy); dy += 19; }
    // --- launcher ---
    var r3 = new RectangleF(r2.Right + 18, y, XR - r2.Right - 18, 240); Glass(g, r3, 14, 150);
    Spaced(g, "LAUNCHER", F(fBold, 13), Cyan, r3.X + 24, r3.Y + 20, 2.6f);
    // toggle
    Txt(g, "Play intro on startup", F(fBold, 14), TextC, r3.X + 24, r3.Y + 60);
    var tg = new RectangleF(r3.Right - 24 - 46, r3.Y + 58, 46, 24);
    using (var gp = Round(tg, 12)) { if (s.Intro) { using var lg = new LinearGradientBrush(new RectangleF(tg.X - 1, tg.Y, tg.Width + 2, tg.Height), Cyan, Blue, 0f); g.FillPath(lg, gp); } else { using var b = new SolidBrush(Color.FromArgb(60, 255, 255, 255)); g.FillPath(b, gp); } }
    using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, s.Intro ? tg.Right - 21 : tg.X + 3, tg.Y + 3, 18, 18);
    AddHit(new RectangleF(r3.X + 16, r3.Y + 50, r3.Width - 32, 40), "intro", () => { s.Intro = !s.Intro; s.Save(); });
    Txt(g, "Launcher music", F(fBold, 14), TextC, r3.X + 24, r3.Y + 110);
    var tm = new RectangleF(r3.Right - 24 - 46, r3.Y + 108, 46, 24);
    using (var gp = Round(tm, 12)) { if (s.Music) { using var lg = new LinearGradientBrush(new RectangleF(tm.X - 1, tm.Y, tm.Width + 2, tm.Height), Cyan, Blue, 0f); g.FillPath(lg, gp); } else { using var b = new SolidBrush(Color.FromArgb(60, 255, 255, 255)); g.FillPath(b, gp); } }
    using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, s.Music ? tm.Right - 21 : tm.X + 3, tm.Y + 3, 18, 18);
    AddHit(new RectangleF(r3.X + 16, r3.Y + 100, r3.Width - 32, 40), "music", () => { s.Music = !s.Music; s.Save(); UpdateMusic(); });
    GhostButton(g, new RectangleF(r3.X + 24, r3.Y + 154, (r3.Width - 60) / 2, 40), "openpack", "PACK FOLDER", "folder", () => { Directory.CreateDirectory(Settings.PackDir); Process.Start(new ProcessStartInfo("explorer.exe", Settings.PackDir) { UseShellExecute = true }); }, 12);
    GhostButton(g, new RectangleF(r3.X + 36 + (r3.Width - 60) / 2, r3.Y + 154, (r3.Width - 60) / 2, 40), "openbg", "BACKGROUNDS", "folder", () => { Directory.CreateDirectory(Settings.BackgroundsDir); Process.Start(new ProcessStartInfo("explorer.exe", Settings.BackgroundsDir) { UseShellExecute = true }); }, 12);
    Txt(g, "Add your own screenshots to Backgrounds.", F(fMed, 12), Dim, r3.X + 24, r3.Y + 156);
    y += 206;
    // --- about ---
    var r4 = new RectangleF(X0, y, full, H - y - 28); Glass(g, r4, 14, 150);
    Spaced(g, "ABOUT & CREDITS", F(fBold, 13), Cyan, r4.X + 24, r4.Y + 20, 2.6f);
    string[] about = {
      $"Kartaclysm launcher v{LauncherVersion} · free fan project · not affiliated with Nintendo. You need your own copy of Mario Kart Wii.",
      "Tracks: custom retro ports by the Custom Mario Kart community — every author is credited on the Tracks page.",
      "Screenshots: Custom Mario Kart Wiiki (mkwiiki.org) track previews — N64 Rainbow Road (cpfusion), Tour Athens Dash (ISwearChris), Tour Vancouver Velocity (Iggy Ze Koopa & SeaOfPixels), RMX Choco Island 1 (ZPL), Tour Ninja Hideaway (Iggy Ze Koopa), SNES Donut Plains 1 (Luca).",
      "Music: Stalemate by KzX (Kayoszx), newgrounds.com/audio/listen/482872 — used with the author's permission.",
      "Fonts: Chakra Petch & Unbounded (SIL Open Font License). Logo and UI: original Kartaclysm artwork.",
    };
    float ay = r4.Y + 50;
    foreach (var p in about) foreach (var l in Wrap(g, p, F(fMed, 13), r4.Width - 48)) { if (ay > r4.Bottom - 22) break; Txt(g, l, F(fMed, 13), Muted, r4.X + 24, ay); ay += 19; }
  }

  // ======================= INTRO =======================
  const float INTRO_END = 3.6f;
  void DrawIntro(Graphics g, float t) {
    hits = new List<Hit>();
    g.Clear(Color.Black);
    const float BAR = 78;
    // phase 2: montage 0.35 – 1.55
    float m0 = 0.35f, cut = 0.4f;
    if (t >= m0 && t < m0 + 3 * cut + 0.25f) {
      int k = Math.Min(2, (int)((t - m0) / cut)); float u = (t - m0 - k * cut) / cut;
      int[] order = { 0, 1, 2 }; var img = shots[(shots.Count - data.shots.Count) + order[k] % data.shots.Count];
      float z = 1.22f - 0.12f * EaseOut(u), vw = img.Width / z, vh = img.Height / z;
      float px = (k % 2 == 0 ? -1 : 1) * 0.5f * (img.Width - vw) * (1 - u) * 0.6f;
      float fadeOut = t > m0 + 3 * cut ? 1 - Clamp01((t - m0 - 3 * cut) / 0.25f) : 1;
      DrawA(g, img, new RectangleF(0, 0, W, H), Clamp01(u * 6) * fadeOut, new RectangleF((img.Width - vw) / 2 + px, (img.Height - vh) / 2, vw, vh));
      using (var b = new SolidBrush(Color.FromArgb(70, 2, 6, 14))) g.FillRectangle(b, 0, 0, W, H);
      // cut flash
      float fl = Clamp01(1 - u / 0.22f) * fadeOut;
      using (var b = new SolidBrush(Color.FromArgb((int)(150 * fl), 200, 250, 255))) g.FillRectangle(b, 0, 0, W, H);
      // HUD caption
      var cap = data.shots[order[k] % data.shots.Count];
      Spaced(g, $"TRACK {k + 1:00}  /  {cap.track.ToUpperInvariant()}", F(fBold, 13), A(Cyan, (int)(255 * fadeOut)), 60, H - BAR + 30, 3);
      var by = "BY " + cap.author.ToUpperInvariant(); float bw = Spaced(g, by, F(fSemi, 12), TextC, 0, 0, 2, false);
      Spaced(g, by, F(fSemi, 12), A(Muted, (int)(255 * fadeOut)), W - 60 - bw, H - BAR + 31, 2);
    }
    // phase 3: logo reveal 1.45 – 3.0
    float l0 = 1.45f;
    if (t >= l0) {
      float u = t - l0;
      var bimg = shotsBlur[(shots.Count - data.shots.Count)];
      DrawA(g, bimg, new RectangleF(0, 0, W, H), Clamp01(u / 0.5f) * 0.55f);
      using (var b = new SolidBrush(Color.FromArgb(120, 2, 4, 10))) g.FillRectangle(b, 0, 0, W, H);
      float symS = 170, wmW = 700, wmH = wmW * wordmark.Height / wordmark.Width, total = symS + 26 + wmW, x0 = (W - total) / 2, cy = H / 2f;
      // symbol: scale + ring flash
      float su = EaseOut(u / 0.45f), sa = Clamp01(u / 0.2f);
      float ss = symS * (0.7f + 0.3f * su);
      float ring = Clamp01(u / 0.7f);
      if (ring < 1) using (var pen = new Pen(A(Cyan, (int)(200 * (1 - ring))), 3 * (1 - ring) + 1)) { float rr = symS * 0.5f + 160 * EaseOut(ring); g.DrawEllipse(pen, x0 + symS / 2 - rr, cy - rr, 2 * rr, 2 * rr); }
      for (int i = 6; i >= 1; i--) using (var b = new SolidBrush(A(Cyan, (int)(6 * sa)))) g.FillEllipse(b, x0 + symS / 2 - ss / 2 - i * 8, cy - ss / 2 - i * 8, ss + i * 16, ss + i * 16);
      DrawA(g, symbol, new RectangleF(x0 + symS / 2 - ss / 2, cy - ss / 2, ss, ss), sa);
      // wordmark wipe + glint
      float wu = EaseOut((u - 0.25f) / 0.55f);
      if (wu > 0) {
        float wx = x0 + symS + 26, wy = cy - wmH / 2 + 4;
        var st = g.Save(); g.SetClip(new RectangleF(wx - 10, wy - 40, (wmW + 20) * wu, wmH + 80));
        DrawA(g, wordmark, new RectangleF(wx + 30 * (1 - wu), wy, wmW, wmH), 1);
        g.Restore(st);
        if (wu < 0.995f) {
          float gx = wx - 10 + (wmW + 20) * wu;
          using var lg = new LinearGradientBrush(new RectangleF(gx - 40, wy, 80, wmH), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f);
          lg.InterpolationColors = new ColorBlend { Colors = new[] { A(Cyan, 0), Color.FromArgb(220, 230, 255, 255), A(Cyan, 0) }, Positions = new[] { 0f, 0.5f, 1f } };
          g.FillRectangle(lg, gx - 40, wy - 20, 80, wmH + 40);
        }
      }
      // underline sweep
      float lu = EaseOut((u - 0.6f) / 0.6f);
      if (lu > 0) {
        float lw = 380 * lu, lx = W / 2f - lw / 2, ly = cy + wmH / 2 + 46;
        using var lg = new LinearGradientBrush(new RectangleF(lx - 1, ly, lw + 2, 2), Cyan, Magenta, 0f); g.FillRectangle(lg, lx, ly, lw, 2);
        var tag = "136 RETRO TRACKS  ·  ONE CLICK TO RACE"; float tw = Spaced(g, tag, F(fBold, 13), TextC, 0, 0, 3.5f, false);
        Spaced(g, tag, F(fBold, 13), A(Muted, (int)(255 * lu)), W / 2f - tw / 2, ly + 14, 3.5f);
      }
    }
    // phase 1: light streak 0 – 0.45
    if (t < 0.55f) {
      float u = EaseOut(t / 0.35f), a = t < 0.35f ? 1 : 1 - (t - 0.35f) / 0.2f;
      float lw = W * u; 
      for (int i = 8; i >= 1; i--) using (var b = new SolidBrush(A(Cyan, (int)(14 * a)))) g.FillRectangle(b, W / 2f - lw / 2, H / 2f - i * 3, lw, i * 6);
      using (var b = new SolidBrush(Color.FromArgb((int)(255 * a), 230, 255, 255))) g.FillRectangle(b, W / 2f - lw / 2, H / 2f - 1, lw, 2);
    }
    // scanlines
    using (var pen = new Pen(Color.FromArgb(18, 0, 0, 0))) for (int y = 0; y < H; y += 3) g.DrawLine(pen, 0, y, W, y);
    // letterbox bars (retract at the end)
    float bu = 1 - Smooth((t - 3.0f) / 0.45f);
    // fade into main UI
    if (t > 3.05f) {
      float a = Smooth((t - 3.05f) / 0.55f);
      pageLayer2 ??= new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
      using (var lg = Graphics.FromImage(pageLayer2)) { Quality(lg); DrawMain(lg, Now, 1); }
      DrawA(g, pageLayer2, new RectangleF(0, 0, W, H), a);
    }
    using (var b = new SolidBrush(Color.Black)) { g.FillRectangle(b, 0, 0, W, BAR * bu); g.FillRectangle(b, 0, H - BAR * bu, W, BAR * bu); }
    if (t < 3.0f) { Spaced(g, "CLICK TO SKIP", F(fBold, 10), Color.FromArgb(90, 255, 255, 255), W - 130, 30, 2.4f); }
    hits = new List<Hit>();
  }
  Bitmap pageLayer2;
}
}
