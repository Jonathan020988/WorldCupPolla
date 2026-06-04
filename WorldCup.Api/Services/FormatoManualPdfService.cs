using System.Globalization;
using System.Text;
using WorldCup.Api.Models;

namespace WorldCup.Api.Services
{
    public sealed class FormatoManualPdfService
    {
        private static readonly CultureInfo CulturaColombia = new("es-CO");

        public byte[] CrearPartidosCompacto(
            string nombrePolla,
            string titulo,
            IReadOnlyList<Partido> partidos)
        {
            var pdf = new PdfDocumentBuilder(PdfPageSize.LetterLandscape);
            var paginas = partidos
                .OrderBy(p => p.Fecha)
                .ThenBy(p => p.Id)
                .Chunk(36)
                .ToList();

            if (paginas.Count == 0)
            {
                pdf.AddPage(canvas =>
                {
                    DibujarEncabezado(canvas, nombrePolla, titulo, "Formato manual");
                    canvas.SetColor(0.96, 0.98, 0.96);
                    canvas.FillRectangle(36, 430, 720, 76);
                    canvas.SetColor(0.12, 0.28, 0.16);
                    canvas.DrawText("Aun no hay partidos disponibles para este formato.", 56, 472, 14, true);
                    canvas.DrawText("Cuando el administrador del torneo genere esta fase, podras descargarla aqui.", 56, 450, 10, false);
                });

                return pdf.Build();
            }

            for (var pagina = 0; pagina < paginas.Count; pagina++)
            {
                var partidosPagina = paginas[pagina].ToList();
                var numeroPagina = pagina + 1;
                var totalPaginas = paginas.Count;

                pdf.AddPage(canvas =>
                {
                    DibujarEncabezado(
                        canvas,
                        nombrePolla,
                        titulo,
                        $"Pagina {numeroPagina} de {totalPaginas}");

                    DibujarTablaPartidos(canvas, partidosPagina);
                });
            }

            return pdf.Build();
        }

        public byte[] CrearClasificacionGrupos(
            string nombrePolla,
            IReadOnlyList<Equipo> equipos)
        {
            var pdf = new PdfDocumentBuilder(PdfPageSize.LetterPortrait);
            var grupos = equipos
                .Where(e => !string.IsNullOrWhiteSpace(e.Grupo))
                .GroupBy(e => e.Grupo.Trim().ToUpperInvariant())
                .OrderBy(g => OrdenGrupo(g.Key))
                .Select(g => new
                {
                    Grupo = g.Key,
                    Equipos = g.OrderBy(e => e.Nombre).ToList()
                })
                .ToList();

            foreach (var paginaGrupos in grupos.Chunk(6))
            {
                pdf.AddPage(canvas =>
                {
                    DibujarEncabezado(
                        canvas,
                        nombrePolla,
                        "Formato manual - Clasificacion de grupos",
                        "Marca la posicion que crees que tendra cada equipo.");

                    var posiciones = new[]
                    {
                        (X: 34.0, Y: 610.0),
                        (X: 314.0, Y: 610.0),
                        (X: 34.0, Y: 420.0),
                        (X: 314.0, Y: 420.0),
                        (X: 34.0, Y: 230.0),
                        (X: 314.0, Y: 230.0)
                    };

                    var index = 0;
                    foreach (var grupo in paginaGrupos)
                    {
                        var posicion = posiciones[index++];
                        DibujarGrupoClasificacion(
                            canvas,
                            grupo.Grupo,
                            grupo.Equipos,
                            posicion.X,
                            posicion.Y);
                    }
                });
            }

            if (grupos.Count == 0)
            {
                pdf.AddPage(canvas =>
                {
                    DibujarEncabezado(
                        canvas,
                        nombrePolla,
                        "Formato manual - Clasificacion de grupos",
                        "No hay equipos cargados.");
                });
            }

            return pdf.Build();
        }

        private static void DibujarEncabezado(
            PdfCanvas canvas,
            string nombrePolla,
            string titulo,
            string subtitulo)
        {
            canvas.SetColor(0.08, 0.30, 0.14);
            canvas.DrawText("WorldCup.App", 34, canvas.Height - 34, 11, true);
            canvas.DrawText(titulo, 34, canvas.Height - 54, 16, true);
            canvas.SetColor(0.28, 0.33, 0.29);
            canvas.DrawText($"Polla: {nombrePolla}", 34, canvas.Height - 72, 9, false);
            canvas.DrawText(subtitulo, canvas.Width - 260, canvas.Height - 54, 9, false);
            canvas.SetStrokeColor(0.49, 0.73, 0.36);
            canvas.Line(34, canvas.Height - 84, canvas.Width - 34, canvas.Height - 84);
        }

        private static void DibujarTablaPartidos(
            PdfCanvas canvas,
            IReadOnlyList<Partido> partidos)
        {
            const double x = 24;
            const double top = 508;
            const double rowHeight = 13.2;
            const double headerHeight = 17;

            var widths = new[] { 30.0, 82.0, 272.0, 176.0, 112.0, 56.0 };
            var headers = new[] { "Id", "Horario", "Partido", "Pronostico", "Resultado", "Puntaje" };

            canvas.SetColor(0.22, 0.57, 0.13);
            canvas.FillRectangle(x, top, widths.Sum(), headerHeight);

            canvas.SetColor(1, 1, 1);
            var currentX = x + 5;
            for (var i = 0; i < headers.Length; i++)
            {
                canvas.DrawText(headers[i], currentX, top + 5, 7.8, true);
                currentX += widths[i];
            }

            var y = top - rowHeight;
            for (var index = 0; index < partidos.Count; index++)
            {
                var partido = partidos[index];
                canvas.SetColor(index % 2 == 0 ? 0.96 : 0.99, 0.99, 0.95);
                canvas.FillRectangle(x, y, widths.Sum(), rowHeight);
                canvas.SetStrokeColor(0.55, 0.78, 0.49);
                canvas.Line(x, y, x + widths.Sum(), y);

                var horario = partido.Fecha.ToString("dd MMM - HH:mm", CulturaColombia);
                var partidoTexto = $"{partido.Local.Nombre} - {partido.Visitante.Nombre}";
                var pronosticoTexto = $"{CodigoEquipo(partido.Local)}";
                var pronosticoVisitante = $"{CodigoEquipo(partido.Visitante)}";

                canvas.SetColor(0.03, 0.14, 0.08);
                canvas.DrawText(partido.Id.ToString(CultureInfo.InvariantCulture), x + 5, y + 4, 7.2, false);
                canvas.DrawText(SinPuntoMes(horario), x + 36, y + 4, 7.2, false);
                canvas.DrawText(Recortar(partidoTexto, 44), x + 118, y + 4, 7.2, true);

                var pronosticoX = x + widths[0] + widths[1] + widths[2] + 8;
                canvas.DrawText(pronosticoTexto, pronosticoX, y + 4, 7.2, false);
                DibujarCajaMarcador(canvas, pronosticoX + 34, y + 2.1);
                canvas.DrawText("-", pronosticoX + 59, y + 4, 7.2, false);
                DibujarCajaMarcador(canvas, pronosticoX + 68, y + 2.1);
                canvas.DrawText(pronosticoVisitante, pronosticoX + 96, y + 4, 7.2, false);

                var resultadoX = pronosticoX + widths[3] + 7;
                DibujarCajaMarcador(canvas, resultadoX, y + 2.1);
                canvas.DrawText("-", resultadoX + 25, y + 4, 7.2, false);
                DibujarCajaMarcador(canvas, resultadoX + 34, y + 2.1);

                var puntajeX = resultadoX + widths[4] + 8;
                canvas.SetStrokeColor(0.55, 0.78, 0.49);
                canvas.Rectangle(puntajeX, y + 2.1, 34, 9);

                y -= rowHeight;
            }

            canvas.SetStrokeColor(0.22, 0.57, 0.13);
            canvas.Rectangle(x, y + rowHeight, widths.Sum(), headerHeight + partidos.Count * rowHeight);
        }

        private static void DibujarGrupoClasificacion(
            PdfCanvas canvas,
            string grupo,
            IReadOnlyList<Equipo> equipos,
            double x,
            double y)
        {
            const double width = 264;
            const double headerHeight = 26;
            const double rowHeight = 25;

            canvas.SetColor(0.22, 0.57, 0.13);
            canvas.FillRectangle(x, y, width, headerHeight);
            canvas.SetColor(1, 1, 1);
            canvas.DrawText($"Grupo {grupo}", x + 10, y + 9, 11, true);

            var rowY = y - rowHeight;
            canvas.SetColor(0.95, 0.99, 0.94);
            canvas.FillRectangle(x, rowY, width, rowHeight);
            canvas.SetColor(0.08, 0.22, 0.12);
            canvas.DrawText("Equipo", x + 10, rowY + 9, 8, true);
            canvas.DrawText("Pos.", x + width - 52, rowY + 9, 8, true);

            rowY -= rowHeight;
            foreach (var equipo in equipos.Take(4))
            {
                canvas.SetColor(1, 1, 1);
                canvas.FillRectangle(x, rowY, width, rowHeight);
                canvas.SetStrokeColor(0.83, 0.91, 0.84);
                canvas.Line(x, rowY, x + width, rowY);
                canvas.SetColor(0.07, 0.11, 0.09);
                canvas.DrawText(Recortar(equipo.Nombre, 27), x + 10, rowY + 9, 9, true);
                canvas.SetStrokeColor(0.36, 0.69, 0.31);
                canvas.Rectangle(x + width - 48, rowY + 5, 26, 15);
                rowY -= rowHeight;
            }

            canvas.SetStrokeColor(0.22, 0.57, 0.13);
            canvas.Rectangle(x, y - (rowHeight * 5), width, headerHeight + rowHeight * 5);
        }

        private static void DibujarCajaMarcador(PdfCanvas canvas, double x, double y)
        {
            canvas.SetStrokeColor(0.36, 0.69, 0.31);
            canvas.Rectangle(x, y, 19, 9);
        }

        private static string CodigoEquipo(Partido partido, bool local)
        {
            return CodigoEquipo(local ? partido.Local : partido.Visitante);
        }

        private static string CodigoEquipo(Equipo equipo)
        {
            return string.IsNullOrWhiteSpace(equipo.CodigoFifa)
                ? Recortar(equipo.Nombre, 3).ToUpperInvariant()
                : equipo.CodigoFifa.ToUpperInvariant();
        }

        private static int OrdenGrupo(string grupo)
        {
            return grupo.Length == 1 && grupo[0] >= 'A' && grupo[0] <= 'Z'
                ? grupo[0] - 'A'
                : 99;
        }

        private static string Recortar(string texto, int maximo)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return "";
            }

            var limpio = LimpiarTexto(texto.Trim());
            return limpio.Length <= maximo
                ? limpio
                : limpio[..Math.Max(0, maximo - 1)] + ".";
        }

        private static string SinPuntoMes(string texto)
        {
            return texto.Replace(".", "", StringComparison.Ordinal);
        }

        private static string LimpiarTexto(string texto)
        {
            return texto
                .Replace("🏆", "", StringComparison.Ordinal)
                .Replace("º", "o", StringComparison.Ordinal)
                .Replace("°", "o", StringComparison.Ordinal)
                .Replace("–", "-", StringComparison.Ordinal)
                .Replace("—", "-", StringComparison.Ordinal);
        }

        private sealed record PdfPageSize(double Width, double Height)
        {
            public static PdfPageSize LetterPortrait { get; } = new(612, 792);
            public static PdfPageSize LetterLandscape { get; } = new(792, 612);
        }

        private sealed class PdfDocumentBuilder
        {
            private readonly PdfPageSize _size;
            private readonly List<string> _streams = new();

            public PdfDocumentBuilder(PdfPageSize size)
            {
                _size = size;
            }

            public void AddPage(Action<PdfCanvas> draw)
            {
                var canvas = new PdfCanvas(_size.Width, _size.Height);
                draw(canvas);
                _streams.Add(canvas.ToString());
            }

            public byte[] Build()
            {
                var objects = new List<byte[]>();
                var pageObjectIds = Enumerable.Range(0, _streams.Count)
                    .Select(i => 5 + i * 2)
                    .ToList();

                objects.Add(Ascii("<< /Type /Catalog /Pages 2 0 R >>"));
                objects.Add(Ascii($"<< /Type /Pages /Kids [{string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"))}] /Count {_streams.Count} >>"));
                objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
                objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"));

                for (var i = 0; i < _streams.Count; i++)
                {
                    var pageId = 5 + i * 2;
                    var streamId = pageId + 1;
                    objects.Add(Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {_size.Width.ToString(CultureInfo.InvariantCulture)} {_size.Height.ToString(CultureInfo.InvariantCulture)}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {streamId} 0 R >>"));

                    var streamBytes = Encoding.Latin1.GetBytes(_streams[i]);
                    var header = Ascii($"<< /Length {streamBytes.Length} >>\nstream\n");
                    var footer = Ascii("\nendstream");
                    objects.Add(Concat(header, streamBytes, footer));
                }

                using var ms = new MemoryStream();
                WriteAscii(ms, "%PDF-1.4\n");
                var offsets = new List<long> { 0 };

                for (var i = 0; i < objects.Count; i++)
                {
                    offsets.Add(ms.Position);
                    WriteAscii(ms, $"{i + 1} 0 obj\n");
                    ms.Write(objects[i]);
                    WriteAscii(ms, "\nendobj\n");
                }

                var xref = ms.Position;
                WriteAscii(ms, $"xref\n0 {objects.Count + 1}\n");
                WriteAscii(ms, "0000000000 65535 f \n");
                foreach (var offset in offsets.Skip(1))
                {
                    WriteAscii(ms, $"{offset:0000000000} 00000 n \n");
                }

                WriteAscii(ms, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
                return ms.ToArray();
            }

            private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

            private static byte[] Concat(params byte[][] arrays)
            {
                var total = arrays.Sum(a => a.Length);
                var result = new byte[total];
                var offset = 0;
                foreach (var array in arrays)
                {
                    Buffer.BlockCopy(array, 0, result, offset, array.Length);
                    offset += array.Length;
                }

                return result;
            }

            private static void WriteAscii(Stream stream, string text)
            {
                var bytes = Encoding.ASCII.GetBytes(text);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        private sealed class PdfCanvas
        {
            private readonly StringBuilder _builder = new();

            public PdfCanvas(double width, double height)
            {
                Width = width;
                Height = height;
            }

            public double Width { get; }
            public double Height { get; }

            public void SetColor(double r, double g, double b)
            {
                _builder.Append(FormattableString.Invariant($"{r:0.###} {g:0.###} {b:0.###} rg\n"));
            }

            public void SetStrokeColor(double r, double g, double b)
            {
                _builder.Append(FormattableString.Invariant($"{r:0.###} {g:0.###} {b:0.###} RG\n"));
            }

            public void FillRectangle(double x, double y, double width, double height)
            {
                _builder.Append(FormattableString.Invariant($"{x:0.##} {y:0.##} {width:0.##} {height:0.##} re f\n"));
            }

            public void Rectangle(double x, double y, double width, double height)
            {
                _builder.Append(FormattableString.Invariant($"{x:0.##} {y:0.##} {width:0.##} {height:0.##} re S\n"));
            }

            public void Line(double x1, double y1, double x2, double y2)
            {
                _builder.Append(FormattableString.Invariant($"{x1:0.##} {y1:0.##} m {x2:0.##} {y2:0.##} l S\n"));
            }

            public void DrawText(string text, double x, double y, double size, bool bold)
            {
                var font = bold ? "F2" : "F1";
                _builder.Append(FormattableString.Invariant($"BT /{font} {size:0.##} Tf {x:0.##} {y:0.##} Td ({Escape(text)}) Tj ET\n"));
            }

            public override string ToString() => _builder.ToString();

            private static string Escape(string text)
            {
                return LimpiarTexto(text)
                    .Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("(", "\\(", StringComparison.Ordinal)
                    .Replace(")", "\\)", StringComparison.Ordinal);
            }
        }
    }
}
