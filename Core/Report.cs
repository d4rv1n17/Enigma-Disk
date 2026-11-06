using System.Text;

namespace EnigmaDisk.Core;

/// <summary>HTML-отчёт: график, рост, типы файлов. Открывается в браузере.</summary>
public static class Report
{
    private static string Esc(string s) => System.Net.WebUtility.HtmlEncode(s);

    public static void Write(string file, SnapshotInfo to, SnapshotInfo? from, GrowthResult? growth,
        List<CategoryVm>? types, DuplicateResult? duplicates)
    {
        var sb = new StringBuilder();
        string drive = to.Drive.TrimEnd('\\');
        string css = """
            :root { color-scheme: dark; }
            body { background:#0f0f0f; color:#f2f2f2; font:14px/1.6 'Segoe UI',sans-serif; margin:0; padding:40px; }
            h1 { font-size:26px; margin:0 0 4px; } h2 { font-size:18px; margin:34px 0 12px; }
            .sub { color:#8e8e8e; } table { border-collapse:collapse; width:100%; max-width:900px; }
            td { padding:8px 10px; border-bottom:1px solid #262626; } td.num { text-align:right; font-variant-numeric:tabular-nums; }
            .acc { color:#ffd200; font-weight:600; } .muted { color:#8e8e8e; }
            .bar { background:#2c2c2c; height:4px; border-radius:2px; width:200px; display:inline-block; vertical-align:middle; }
            .bar i { display:block; height:4px; border-radius:2px; background:#ffd200; }
            .card { background:#181818; border-radius:12px; padding:18px 20px; margin:0 0 14px; max-width:900px; }
            footer { color:#5c5c5c; margin-top:40px; font-size:12px; }
            """;

        sb.Append("<!doctype html><html><head><meta charset='utf-8'><title>").Append(Esc($"Enigma Disk — {drive}"))
          .Append("</title><style>").Append(css).Append("</style></head><body>");
        sb.Append("<h1>Enigma Disk — ").Append(Esc(drive)).Append("</h1>");
        sb.Append("<div class='sub'>").Append(Esc(Loc.F("rp.generated", Format.When(to.Time)))).Append("</div>");

        // сводка
        sb.Append("<h2>").Append(Esc(Loc.T("rp.usedSpace"))).Append("</h2><div class='card'>");
        sb.Append("<table><tr><td>").Append(Esc(Loc.T("rp.totalUsed"))).Append("</td><td class='num acc'>")
          .Append(Esc(Format.Size(to.UsedBytes))).Append("</td></tr>");
        sb.Append("<tr><td>").Append(Esc(Loc.T("rp.free"))).Append("</td><td class='num'>")
          .Append(Esc(Format.Size(to.FreeBytes))).Append("</td></tr>");
        if (from != null)
        {
            long d = to.UsedBytes - from.UsedBytes;
            sb.Append("<tr><td>").Append(Esc(Loc.F("rp.period", Format.When(from.Time)))).Append("</td><td class='num ")
              .Append(d >= 0 ? "acc" : "muted").Append("'>").Append(Esc(Format.Delta(d))).Append("</td></tr>");
        }
        sb.Append("</table></div>");

        // рост
        if (growth != null)
        {
            foreach (var (title, list) in new[] { (Loc.T("rp.grew"), growth.Grew), (Loc.T("rp.shrank"), growth.Shrank) })
            {
                if (list.Count == 0) continue;
                double max = list[0] is { } f0 ? Math.Max(1, Math.Abs(f0.Delta)) : 1;
                sb.Append("<h2>").Append(Esc(title)).Append("</h2><table>");
                foreach (var g in list.Take(50))
                {
                    double frac = Math.Min(1, Math.Abs(g.Delta) / max) * 100;
                    sb.Append("<tr><td>").Append(Esc(g.Path))
                      .Append("</td><td class='num'><span class='bar'><i style='width:").Append(frac.ToString("0"))
                      .Append("%'></i></span></td><td class='num acc'>").Append(Esc(g.DeltaText)).Append("</td></tr>");
                }
                sb.Append("</table>");
            }
        }

        // типы файлов
        if (types is { Count: > 0 })
        {
            sb.Append("<h2>").Append(Esc(Loc.T("rp.types"))).Append("</h2><table>");
            double max = types[0].Size;
            foreach (var t in types)
            {
                sb.Append("<tr><td>").Append(Esc(t.Name)).Append("</td><td class='num'><span class='bar'><i style='width:")
                  .Append(Math.Min(100, t.Size * 100.0 / Math.Max(1, max)).ToString("0")).Append("%'></i></span></td>")
                  .Append("<td class='num acc'>").Append(Esc(t.SizeText)).Append("</td></tr>");
            }
            sb.Append("</table>");
        }

        // дубликаты
        if (duplicates is { Groups.Count: > 0 })
        {
            sb.Append("<h2>").Append(Esc(Loc.T("nav.duplicates"))).Append("</h2><table>");
            foreach (var g in duplicates.Groups.Take(30))
            {
                sb.Append("<tr><td>").Append(Esc(g.Name))
                  .Append("<div class='sub'>").Append(Esc(g.CopiesText)).Append(" · ")
                  .Append(Esc(Loc.T("du.waste"))).Append(" ").Append(Esc(g.WasteText)).Append("</div></td>")
                  .Append("<td class='num acc'>").Append(Esc(g.SizeText)).Append("</td></tr>");
                foreach (var p in g.Paths.Skip(1))
                    sb.Append("<tr><td class='muted' style='padding-left:24px'>").Append(Esc(p)).Append("</td><td></td></tr>");
            }
            sb.Append("</table>");
        }

        sb.Append("<footer>Enigma Disk 2.0 · ").Append(Esc(Format.When(DateTime.Now))).Append("</footer>");
        sb.Append("</body></html>");
        File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
    }
}
