using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LiveVisualizer
{
	public class WaveformHandling
	{
		// ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ Attributes ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ \\
		public PictureBox BoxWaveform;
		public PictureBox BoxVisualization;

		public Color Fore = Color.Black;
		public Color Back = Color.White;
		public Color Graph = Color.FromName("HotTrack");




		// ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ Constructor ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ \\
		public WaveformHandling(PictureBox waveBox, PictureBox visualBox)
		{
			// Set Attributes
			BoxWaveform = waveBox;
			BoxVisualization = visualBox;
		}





		// ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ Methods ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ \\
		public Bitmap DrawWaveform(float[] floats, long offset = 0, int samplesPerPixel = 1)
		{
			// Erstelle ein neues Bitmap mit der Größe der PictureBox
			Bitmap waveformBitmap = new(BoxWaveform.Width, BoxWaveform.Height);

			// Hole das Graphics-Objekt aus dem Bitmap
			using Graphics g = Graphics.FromImage(waveformBitmap);

			// Hintergrund löschen
			g.Clear(Back);

			// Höhe und Mitte der PictureBox berechnen
			int height = BoxWaveform.Height;
			int centerY = height / 2;

			// Pen für die Wellenform initialisieren
			using Pen pen = new(Graph);

			// Zeichne die Wellenform
			for (int x = 0; x < BoxWaveform.Width; x++)
			{
				// Berechne den Bereich der Samples für diesen Pixel
				long startSampleIndex = offset + x * samplesPerPixel;
				long endSampleIndex = startSampleIndex + samplesPerPixel;

				// Validierung: Bereich muss innerhalb der Array-Grenzen liegen
				if (startSampleIndex >= floats.Length || startSampleIndex < 0) continue;
				endSampleIndex = Math.Min(endSampleIndex, floats.Length);

				// Finde Min- und Max-Werte für den aktuellen Bereich
				float min = 0, max = 0;
				for (long i = startSampleIndex; i < endSampleIndex; i++)
				{
					min = Math.Min(min, floats[i]);
					max = Math.Max(max, floats[i]);
				}

				// Skaliere Min- und Max-Werte auf die Höhe des Bildes
				int yMin = centerY - (int) (min * centerY);
				int yMax = centerY - (int) (max * centerY);

				// Zeichne die vertikale Linie für diesen Pixel
				g.DrawLine(pen, x, yMin, x, yMax);
			}

			// Bitmap zurückgeben
			return waveformBitmap;
		}

		public Bitmap DrawWaveformSmooth(float[] floats, long offset = 0, int samplesPerPixel = 1)
		{
			// Überprüfen, ob floats und die PictureBox gültig sind
			if (floats.Length == 0 || BoxWaveform.Width <= 0 || BoxWaveform.Height <= 0)
			{
				return new Bitmap(1, 1);
			}

			// Farben aus der PictureBox übernehmen
			Color waveformColor = Graph;
			Color backgroundColor = Back;

			Bitmap bmp = new Bitmap(BoxWaveform.Width, BoxWaveform.Height);
			using Graphics gfx = Graphics.FromImage(bmp);
			using Pen pen = new Pen(waveformColor);
			gfx.SmoothingMode = SmoothingMode.AntiAlias;
			gfx.Clear(backgroundColor);

			float centerY = BoxWaveform.Height / 2f;
			float yScale = BoxWaveform.Height / 2f;

			for (int x = 0; x < BoxWaveform.Width; x++)
			{
				long sampleIndex = offset + (long) x * samplesPerPixel;

				if (sampleIndex >= floats.Length)
				{
					break;
				}

				float maxValue = float.MinValue;
				float minValue = float.MaxValue;

				for (int i = 0; i < samplesPerPixel; i++)
				{
					if (sampleIndex + i < floats.Length)
					{
						maxValue = Math.Max(maxValue, floats[sampleIndex + i]);
						minValue = Math.Min(minValue, floats[sampleIndex + i]);
					}
				}

				float yMax = centerY - maxValue * yScale;
				float yMin = centerY - minValue * yScale;

				// Überprüfen, ob die Werte innerhalb des sichtbaren Bereichs liegen
				if (yMax < 0) yMax = 0;
				if (yMin > BoxWaveform.Height) yMin = BoxWaveform.Height;

				// Zeichne die Linie nur, wenn sie sichtbar ist
				if (Math.Abs(yMax - yMin) > 0.01f)
				{
					gfx.DrawLine(pen, x, yMax, x, yMin);
				}
				else if (samplesPerPixel == 1)
				{
					// Zeichne einen Punkt, wenn samplesPerPixel 1 ist und die Linie zu klein ist
					gfx.DrawLine(pen, x, centerY, x, centerY - floats[sampleIndex] * yScale);
				}
			}

			return bmp;
		}

		public Bitmap DrawWaveformSolid(float[] floats, long offset = 0, int samplesPerPixel = 1)
		{
			if (floats.Length == 0 || BoxWaveform.Width <= 0 || BoxWaveform.Height <= 0)
			{
				return new Bitmap(1, 1);
			}

			Color waveformColor = Graph;
			Color backgroundColor = Back;

			Bitmap bmp = new Bitmap(BoxWaveform.Width, BoxWaveform.Height);
			using Graphics gfx = Graphics.FromImage(bmp);
			using SolidBrush brush = new SolidBrush(waveformColor);
			gfx.SmoothingMode = SmoothingMode.AntiAlias;
			gfx.Clear(backgroundColor);

			float centerY = BoxWaveform.Height / 2f;
			float yScale = BoxWaveform.Height / 2f;

			PointF[] points = new PointF[BoxWaveform.Width]; // Array für die Punkte der Wellenform

			for (int x = 0; x < BoxWaveform.Width; x++)
			{
				long sampleIndex = offset + (long) x * samplesPerPixel;

				if (sampleIndex >= floats.Length)
				{
					break;
				}
				float averageValue = 0;
				for (int i = 0; i < samplesPerPixel; i++)
				{
					if (sampleIndex + i < floats.Length)
					{
						averageValue += floats[sampleIndex + i];
					}
				}
				averageValue /= samplesPerPixel;
				float y = centerY - averageValue * yScale;

				if (float.IsNaN(y) || float.IsInfinity(y))
				{
					continue;//Ungültigen Wert überspringen
				}
				if (y < 0)
					y = 0;
				if (y > BoxWaveform.Height)
					y = BoxWaveform.Height;
				points[x] = new PointF(x, y); // Punkt im Array speichern
			}

			// Pfad erstellen und füllen
			if (points.Length > 0)
			{
				// Erstelle einen GraphicsPath und füge die Punkte hinzu
				using GraphicsPath path = new GraphicsPath();
				path.AddLines(points);
				// Füge einen unteren Rand hinzu, um die Fläche zu schließen
				path.AddLine(points[^1].X, centerY, points[0].X, centerY);
				gfx.FillPath(brush, path);
			}

			return bmp;
		}






		// ~~~~~ ~~~~~ ~~~~~ Barform ~~~~~ ~~~~~ ~~~~~ \\
		public Bitmap DrawBarform(float[] floats, long sample, int samplesPerPixel, int bars = 16, int sensitivity = 50)
		{
			// Sensitivity in den Bereich 0 bis 100 beschränken
			sensitivity = Math.Clamp(sensitivity, 0, 100);

			// Erstelle ein neues Bitmap mit der Größe der PictureBox
			Bitmap barformBitmap = new(BoxVisualization.Width, BoxVisualization.Height);

			// Hole das Graphics-Objekt aus dem Bitmap
			using Graphics g = Graphics.FromImage(barformBitmap);

			// Hintergrund löschen
			g.Clear(Back);

			// Breite und Höhe der Box
			int width = BoxVisualization.Width;
			int height = BoxVisualization.Height;

			// Breite jeder Bar berechnen (Integer-Arithmetik, um Ungenauigkeiten zu vermeiden)
			int barWidth = width / bars;

			// Restbreite berücksichtigen (falls width nicht durch bars teilbar ist)
			int remainingWidth = width % bars;

			// Sensitivity-Skalierungsfaktor (0 → sehr schwach, 100 → volle Amplitude)
			float sensitivityFactor = sensitivity / 100f;

			// Zeichne die Bars
			for (int i = 0; i < bars; i++)
			{
				// Bereich der Samples für die aktuelle Bar berechnen
				long startSampleIndex = sample + i * samplesPerPixel;
				long endSampleIndex = Math.Min(startSampleIndex + samplesPerPixel, floats.Length);

				// Bereich prüfen, ob Samples verfügbar sind
				if (startSampleIndex >= 0 && endSampleIndex > startSampleIndex && startSampleIndex < floats.Length)
				{
					// Maximalen Absolutwert in diesem Bereich berechnen
					float maxAmplitude = 0;
					for (long j = startSampleIndex; j < endSampleIndex; j++)
					{
						maxAmplitude = Math.Max(maxAmplitude, Math.Abs(floats[j]));
					}

					// Skaliere die Amplitude basierend auf der Sensitivity
					maxAmplitude *= sensitivityFactor;

					// Höhe der Bar berechnen (normalisiert)
					int barHeight = (int) (maxAmplitude * height);

					// Berechne die X-Position der Bar
					int xPosition = i * barWidth + Math.Min(i, remainingWidth);

					// Zeichne die Bar (Reduzierte Breite für Abstand zwischen Bars)
					g.FillRectangle(new SolidBrush(Graph), xPosition, height - barHeight, barWidth + (i < remainingWidth ? 1 : 0), barHeight);
				}
			}

			// Bitmap zurückgeben
			return barformBitmap;
		}

















	}
}
