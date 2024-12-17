using System.IO.Compression;
using NAudio.Wave;
using Timer = System.Windows.Forms.Timer;

namespace LiveVisualizer
{
	public partial class WindowMain : Form
	{
		// ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ ATTRIBUTES ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ \\
		public string Repopath;

		public AudioHandling AuH;
		public WaveformHandling WfH;
		public SelectionHandling SeH;
		public CuttingHandling CuH;
		public StretchingHandling StH;


		public long OffsetTrack;
		public long OffsetSample;
		public int SamplesPerPixel = 1;



		private Timer updateTrackTimer;
		private Timer updateSampleTimer;
		private Timer visualizationTimer;
		private bool doesLoop = false;
		private int lastWindowsize = 1024;



		// ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ CONSTRUCTOR ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ \\
		public WindowMain()
		{
			InitializeComponent();

			// Set window position to primary screen top left corner
			StartPosition = FormStartPosition.Manual;
			Location = new Point(0, 0);

			// Get repopath
			Repopath = GetRepopath(true);

			// Init. objects
			AuH = new AudioHandling();
			WfH = new WaveformHandling(pictureBox_waveform, pictureBox_visualizer);
			SeH = new SelectionHandling(pictureBox_waveform, pictureBox_sample);
			CuH = new CuttingHandling();
			StH = new StretchingHandling();

			// Init. timers
			updateTrackTimer = new Timer { Interval = (int) numericUpDown_delay.Value + 1 }; // Setzen Sie das Intervall auf 1000 ms (1 Sekunde) oder einen anderen gewünschten Wert
			updateTrackTimer.Tick += (_, _) => UpdateTrackView();

			updateSampleTimer = new Timer { Interval = (int) numericUpDown_delay.Value + 1 };
			updateSampleTimer.Tick += (_, _) => UpdateSampleView();

			visualizationTimer = new Timer { Interval = 1000 / (int) numericUpDown_fps.Value }; // Setzen Sie das Intervall auf 1000 ms (1 Sekunde) oder einen anderen gewünschten Wert
			visualizationTimer.Tick += (_, _) => UpdateVisualization();


			// Register events
			pictureBox_waveform.DoubleClick += ImportAudioFile;
			pictureBox_sample.DoubleClick += ImportAudioFile;
			button_import.Click += ImportAudioFile;
			pictureBox_waveform.MouseWheel += ScrollOffset;
			pictureBox_sample.MouseWheel += ScrollOffset;
			pictureBox_waveform.MouseDown += pictureBox_waveform_MouseDown;
			pictureBox_waveform.MouseMove += pictureBox_waveform_MouseMove;
			pictureBox_waveform.MouseUp += pictureBox_waveform_MouseUp;
			pictureBox_waveform.Paint += pictureBox_waveform_Paint;
			pictureBox_sample.MouseDown += pictureBox_sample_MouseDown;
			pictureBox_sample.MouseMove += pictureBox_sample_MouseMove;
			pictureBox_sample.MouseUp += pictureBox_sample_MouseUp;
			pictureBox_sample.Paint += pictureBox_sample_Paint;

			// Update track view
			UpdateTrackView();
			UpdateSampleView();
		}




		// ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ METHODS ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ \\
		public string GetRepopath(bool root = false)
		{
			string dirpath = AppDomain.CurrentDomain.BaseDirectory;

			if (root)
			{
				dirpath += @"..\..\..\";
			}

			dirpath = Path.GetFullPath(dirpath);

			return dirpath;

		}

		public void ImportAudioFile(object? sender, EventArgs e)
		{
			OpenFileDialog ofd = new();
			ofd.Title = "Select audio file(s)";
			ofd.Filter = "Audio Files|*.wav;*.mp3;";
			ofd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
			ofd.Multiselect = true;

			if (ofd.ShowDialog() == DialogResult.OK)
			{
				foreach (string f in ofd.FileNames)
				{
					// Falls sender pictureBox_waveform ist, wird die Datei als Track importiert
					if (sender == pictureBox_waveform || sender == button_import)
					{
						AuH.LoadSample(f, "t");
					}

					// Falls sender pictureBox_sample ist, wird die Datei als Sample importiert
					if (sender == pictureBox_sample)
					{
						AuH.LoadSample(f, "s");
					}

					// Update track view
					UpdateTrackView();
					UpdateSampleView();

					numericUpDown_id.Value = AuH.Tracks.Count;
					numericUpDown_idSample.Value = AuH.Samples.Count;

				}
			}


		}


		// ~~~~~ ~~~~~ ~~~~~ Views updates ~~~~~ ~~~~~ ~~~~~ \\
		public void UpdateTrackView()
		{
			// Set numericUpDown_id max
			numericUpDown_id.Maximum = AuH.Tracks.Count;

			// Get id (0-based)
			int id = (int) numericUpDown_id.Value - 1;

			// Abort if id is out of range
			if (id < 0 || id >= AuH.Tracks.Count)
			{
				// PictureBoxes clear
				pictureBox_waveform.Image = null;
				pictureBox_visualizer.Image = null;
				pictureBox_sample.Image = null;

				// Show label double click info
				label_doubleclickinfo.Visible = true;
				label_trackname.Text = "Track name";
				label_trackmeta.Text = "Sample rate | Bit depth | Channels";

				return;
			}

			// Hide label double click info
			label_doubleclickinfo.Visible = false;

			// Get selected SampleObject
			SampleObject selectedSample = AuH.Tracks[id];

			// Update labels name & metadata
			label_trackname.Text = selectedSample.Name;
			label_trackmeta.Text = $"{selectedSample.Sampletrate}Hz | {selectedSample.Bitdepth}Bit | {selectedSample.Channels}ch";
			// Align label on top left side of picturebox
			label_trackmeta.Location = new Point(pictureBox_waveform.Right - label_trackmeta.Width, pictureBox_waveform.Top);
			label_trackmeta.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

			// Set length
			textBox_length.Text = selectedSample.Length + "\n" + AuH.GetTimestamp(selectedSample.Length * selectedSample.Channels * 2);

			// If playing, show current position
			if (selectedSample.WoE.PlaybackState == PlaybackState.Playing)
			{
				// Get timestamp
				string timestamp = AuH.GetTimestamp();
				textBox_timestamp.Text = timestamp;

				// Get current sample
				long current = AuH.GetCurrent()[0] * 2;
				textBox_offsetTrack.Text = selectedSample.Channels + " * " + current / 2;

				// Set waveform at offset if sync checked
				if (checkBox_sync.Checked)
				{
					OffsetTrack = current;
				}
				else
				{
					OffsetTrack = 0;
				}
			}
			else
			{
				// Show current offset
				textBox_offsetTrack.Text = selectedSample.Channels + " * " + OffsetTrack / 2;

				// Show current timestamp
				textBox_timestamp.Text = AuH.GetTimestamp(OffsetTrack);
			}

			// Draw waveform
			DrawWaveforms();
		}

		private void UpdateSampleView()
		{
			// Set numericUpDown_idSample max
			numericUpDown_idSample.Maximum = AuH.Samples.Count;

			// Get id (0-based)
			int id = (int) numericUpDown_idSample.Value - 1;

			// Abort if id is out of range
			if (id < 0 || id >= AuH.Samples.Count)
			{
				pictureBox_sample.Image = null;
				textBox_samplename.Text = "Sample name";
				label_samplemeta.Text = "Sample rate | Bit depth | Channels";
				textBox_lengthSample.Text = "Length" + "\n" + "00:00:00.000";
				return;
			}

			// Get selected SampleObject
			SampleObject selectedSample = AuH.Samples[id];

			// Update labels name & metadata
			textBox_samplename.Text = selectedSample.Name;
			label_samplemeta.Text = $"{selectedSample.Sampletrate}Hz | {selectedSample.Bitdepth}Bit | {selectedSample.Channels}ch";
			// Align label on top left side of picturebox
			label_samplemeta.Location = new Point(pictureBox_sample.Right - label_samplemeta.Width, pictureBox_sample.Top);
			label_samplemeta.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

			// Set length
			textBox_lengthSample.Text = selectedSample.Length + "\n" + AuH.GetTimestamp(selectedSample.Length * selectedSample.Channels * 2);

			// If playing, show current position
			if (selectedSample.WoE.PlaybackState == PlaybackState.Playing)
			{
				// Get current sample
				long current = AuH.GetCurrent()[1] * 2;

				// Set waveform at offset if sync checked
				if (checkBox_sync.Checked)
				{
					OffsetSample = current;
				}
				else
				{
					OffsetSample = 0;
				}

				// Get timestamp
				string timestamp = AuH.GetTimestamp(current, false);
				textBox_timestampSample.Text = timestamp;

				// Show current offset
				textBox_offsetSample.Text = selectedSample.Channels + " * " + current / 2;
			}
			else
			{
				// Show current offset
				textBox_offsetSample.Text = selectedSample.Channels + " * " + OffsetSample / 2;

				// Show current timestamp
				textBox_timestampSample.Text = AuH.GetTimestamp(OffsetSample, false);
			}

			// Draw waveform
			DrawWaveforms();
		}

		private void UpdateVisualization()
		{
			// Get id (0-based)
			int id = (int) numericUpDown_id.Value - 1;

			// Abort if id is out of range
			if (id < 0 || id >= AuH.Tracks.Count)
			{
				return;
			}

			// Get selected SampleObject
			SampleObject selectedSample = AuH.Tracks[id];

			// If checked show spectrum
			if (checkBox_live.Checked)
			{
				// Draw spectrum
				pictureBox_visualizer.Image = WfH.DrawBarform(selectedSample.Floats, AuH.GetCurrent()[0] * 2, SamplesPerPixel, (int) numericUpDown_bars.Value, (int) numericUpDown_sensitivity.Value);
			}
		}

		private void DrawWaveforms()
		{
			// Get id (0-based)
			int track = (int) numericUpDown_id.Value - 1;
			int sample = (int) numericUpDown_idSample.Value - 1;

			// If combobox text or selected item == Default, use default function
			if (comboBox_waveform.SelectedIndex == 0)
			{
				// Draw waveform if each index is valid
				if (track >= 0 && track < AuH.Tracks.Count)
				{
					pictureBox_waveform.Image =
						WfH.DrawWaveform(AuH.Tracks[track].Floats, OffsetTrack, SamplesPerPixel);
				}

				if (sample >= 0 && sample < AuH.Samples.Count)
				{
					pictureBox_sample.Image =
						WfH.DrawWaveform(AuH.Samples[sample].Floats, OffsetSample, SamplesPerPixel);
				}
			}
			else if (comboBox_waveform.SelectedIndex == 1)
			{
				// Draw waveform if each index is valid
				if (track >= 0 && track < AuH.Tracks.Count)
				{
					pictureBox_waveform.Image =
						WfH.DrawWaveformSmooth(AuH.Tracks[track].Floats, OffsetTrack, SamplesPerPixel);
				}

				if (sample >= 0 && sample < AuH.Samples.Count)
				{
					pictureBox_sample.Image =
						WfH.DrawWaveformSmooth(AuH.Samples[sample].Floats, OffsetSample, SamplesPerPixel);
				}
			}
			else if (comboBox_waveform.SelectedIndex == 2)
			{
				// Draw waveform if each index is valid
				if (track >= 0 && track < AuH.Tracks.Count)
				{
					pictureBox_waveform.Image =
						WfH.DrawWaveformSolid(AuH.Tracks[track].Floats, OffsetTrack, SamplesPerPixel);
				}

				if (sample >= 0 && sample < AuH.Samples.Count)
				{
					pictureBox_sample.Image =
						WfH.DrawWaveformSolid(AuH.Samples[sample].Floats, OffsetSample, SamplesPerPixel);
				}
			}

			pictureBox_waveform.Refresh();
			pictureBox_sample.Refresh();
		}

		// ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ EVENTS ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ \\
		private void button_playStop_Click(object? sender, EventArgs e)
		{
			// Get id (0-based)
			int id = (int) numericUpDown_id.Value - 1;

			// Abort if id is out of range
			if (id < 0 || id >= AuH.Tracks.Count)
			{
				return;
			}

			// Check if the button is currently showing the play symbol
			if (button_playStop.Text == @"▶")
			{
				// Get selected SampleObject
				SampleObject selectedSample = AuH.Tracks[id];

				// Play the selected sample
				visualizationTimer.Start();
				updateTrackTimer.Start();
				selectedSample.Play((int) numericUpDown_speed.Value);

				// Update button text to stop symbol
				button_playStop.Text = @"■";

				// Subscribe to playback stopped event to reset button text and position
				selectedSample.WoE.PlaybackStopped += (_, _) =>
				{
					button_playStop.Text = @"▶";
					OffsetTrack = 0;
					visualizationTimer.Stop();
					updateTrackTimer.Stop();
					UpdateTrackView();
				};
			}
			else
			{
				// Get selected SampleObject
				SampleObject selectedSample = AuH.Tracks[id];

				// Stop the selected sample
				selectedSample.Stop();

				// Update button text to play symbol
				button_playStop.Text = @"▶";
				visualizationTimer.Stop();
				updateTrackTimer.Stop();
			}
		}

		private void button_playStopSample_Click(object? sender, EventArgs e)
		{
			// Get id (0-based)
			int id = (int) numericUpDown_idSample.Value - 1;

			// Abort if id is out of range
			if (id < 0 || id >= AuH.Samples.Count)
			{
				return;
			}

			// Check if the button is currently showing the play symbol
			if (button_playStopSample.Text == @"▶")
			{
				// Get selected SampleObject
				SampleObject selectedSample = AuH.Samples[id];

				// Play the selected sample with loop if doesLoop is true
				updateSampleTimer.Start();
				selectedSample.PlayWithLoop((int) numericUpDown_speedSample.Value, doesLoop);

				// Update button text to stop symbol
				button_playStopSample.Text = @"■";

				// Subscribe to playback stopped event to reset button text and position
				selectedSample.WoE.PlaybackStopped += (_, _) =>
				{
					if (!doesLoop)
					{
						button_playStopSample.Text = @"▶";
						OffsetSample = 0;
						updateSampleTimer.Stop();
						UpdateSampleView(); // Ensure the sample view is updated when playback stops
					}
				};
			}
			else
			{
				// Get selected SampleObject
				SampleObject selectedSample = AuH.Samples[id];

				// Stop the selected sample
				selectedSample.Stop();

				// Update button text to play symbol
				button_playStopSample.Text = @"▶";
				updateSampleTimer.Stop();
			}
		}

		// ~~~~~ ~~~~~ ~~~~~ Track / sample selection ~~~~~ ~~~~~ ~~~~~ \\
		private void numericUpDown_id_ValueChanged(object sender, EventArgs e)
		{
			// Stop all other tracks
			foreach (var sample in AuH.Tracks)
			{
				if (sample.WoE.PlaybackState == PlaybackState.Playing)
				{
					sample.Stop();
				}
			}

			// Update track view
			UpdateTrackView();
		}

		private void numericUpDown_idSample_ValueChanged(object? sender, EventArgs eventArgs)
		{
			// Stop all other samples
			foreach (var sample in AuH.Samples)
			{
				if (sample.WoE.PlaybackState == PlaybackState.Playing)
				{
					sample.Stop();
				}
			}

			// Update sample view
			UpdateSampleView();

			// If autoplay is checked, play the new sample
			if (checkBox_autoplay.Checked)
			{
				// Get id (0-based)
				int id = (int) numericUpDown_idSample.Value - 1;

				// Abort if id is out of range
				if (id < 0 || id >= AuH.Samples.Count)
				{
					return;
				}

				// Get selected SampleObject
				SampleObject selectedSample = AuH.Samples[id];

				// Play the selected sample
				updateSampleTimer.Start();
				selectedSample.PlayWithLoop((int) numericUpDown_speedSample.Value, doesLoop);

				// Update button text to stop symbol
				button_playStopSample.Text = @"■";

				// Ensure the sample view is updated while playing
				updateSampleTimer.Tick += (_, _) => UpdateSampleView();

				// Subscribe to playback stopped event to reset button text and position
				selectedSample.WoE.PlaybackStopped += (_, _) =>
				{
					if (!doesLoop)
					{
						button_playStopSample.Text = @"▶";
						OffsetSample = 0;
						updateSampleTimer.Stop();
						UpdateSampleView(); // Ensure the sample view is updated when playback stops
					}
				};
			}
		}

		// ~~~~~ ~~~~~ ~~~~~ Zoom, refresh rate, delay ~~~~~ ~~~~~ ~~~~~ \\
		private void numericUpDown_delay_ValueChanged(object sender, EventArgs e)
		{
			// Update timer interval
			updateTrackTimer.Interval = (int) numericUpDown_delay.Value + 1;
		}

		private void numericUpDown_fps_ValueChanged(object sender, EventArgs e)
		{
			// Update timer interval
			visualizationTimer.Interval = 1000 / (int) numericUpDown_fps.Value;
		}

		private void numericUpDown_zoom_ValueChanged(object sender, EventArgs e)
		{
			// Update samples per pixel
			SamplesPerPixel = (int) numericUpDown_zoom.Value;

			// Update track view
			UpdateTrackView();
			UpdateSampleView();
		}


		// ~~~~~ ~~~~~ ~~~~~ Sync, scroll, reset ~~~~~ ~~~~~ ~~~~~ \\
		private void ScrollOffset(object? sender, MouseEventArgs e)
		{
			// Wenn STRG gedrückt ist, wird gezoomt
			if (ModifierKeys == Keys.Control)
			{
				if (e.Delta > 0 && numericUpDown_zoom.Value > 1) // Herauszoomen (Zoom verkleinern)
				{
					numericUpDown_zoom.Value = Math.Max(1, (int) ((float) numericUpDown_zoom.Value / 2));
				}
				else if (e.Delta < 0 && numericUpDown_zoom.Value < numericUpDown_zoom.Maximum) // Hereinzoomen (Zoom vergrößern)
				{
					numericUpDown_zoom.Value = Math.Min(numericUpDown_zoom.Maximum, (int) ((float) numericUpDown_zoom.Value * 2));
				}

				return;
			}

			// If sender is pictureBox_waveform, scroll track
			if (sender == pictureBox_waveform)
			{
				// Abort if Id is invalid
				if (numericUpDown_id.Value < 1 || numericUpDown_id.Value > AuH.Tracks.Count)
				{
					return;
				}

				// Wenn STRG nicht gedrückt ist, wird der OffsetTrack verschoben
				OffsetTrack += e.Delta / 4 * SamplesPerPixel;
				// Keep offset in range
				OffsetTrack = Math.Max(0, OffsetTrack);
				OffsetTrack = Math.Min(OffsetTrack, AuH.Tracks[(int) numericUpDown_id.Value - 1].Floats.Length - SamplesPerPixel);
			}

			// If sender is pictureBox_sample, scroll sample
			if (sender == pictureBox_sample)
			{
				// Abort if Id is invalid
				if (numericUpDown_idSample.Value < 1 || numericUpDown_idSample.Value > AuH.Samples.Count)
				{
					return;
				}

				// Wenn STRG nicht gedrückt ist, wird der OffsetSample verschoben
				OffsetSample += e.Delta / 4 * SamplesPerPixel;
				// Keep offset in range
				OffsetSample = Math.Max(0, OffsetSample);
				OffsetSample = Math.Min(OffsetSample, AuH.Samples[(int) numericUpDown_idSample.Value - 1].Floats.Length - SamplesPerPixel);
			}

			// Update track view
			UpdateTrackView();
			UpdateSampleView();
		}

		private void textBox_samplename_TextChanged(object sender, EventArgs e)
		{
			// Get id (0-based)
			int id = (int) numericUpDown_idSample.Value - 1;

			// Abort if id is out of range
			if (id < 0 || id >= AuH.Samples.Count)
			{
				return;
			}

			// Update name
			AuH.Samples[id].Name = textBox_samplename.Text;

			// Update track view
			UpdateSampleView();
		}


		// ~~~~~ ~~~~~ ~~~~~ Selection track / sample ~~~~~ ~~~~~ ~~~~~ \\
		private void pictureBox_waveform_MouseDown(object? sender, MouseEventArgs e)
		{
			// Abort if any track is playing or if id is invalid
			if (AuH.Tracks.Any(track => track.WoE.PlaybackState == PlaybackState.Playing) || numericUpDown_id.Value < 1 || numericUpDown_id.Value > AuH.Tracks.Count)
			{
				return;
			}

			// If left mouse button & CTRL pressed, select

			if (e.Button == MouseButtons.Left && ModifierKeys.HasFlag(Keys.Control))
			{
				Point relativePoint = pictureBox_waveform.PointToClient(Cursor.Position);
				SeH.StartSelection(relativePoint, pictureBox_waveform);
				Console.WriteLine($"MouseDown Relative Point X:{relativePoint.X}");
			}
		}

		private void pictureBox_waveform_MouseMove(object? sender, MouseEventArgs e)
		{
			// Abort if any track is playing or if id is invalid
			if (AuH.Tracks.Any(track => track.WoE.PlaybackState == PlaybackState.Playing) || numericUpDown_id.Value < 1 || numericUpDown_id.Value > AuH.Tracks.Count)
			{
				return;
			}

			if (SeH.IsSelecting)
			{
				Point relativePoint = pictureBox_waveform.PointToClient(Cursor.Position);
				SeH.UpdateSelection(relativePoint);
				Console.WriteLine($"MouseMove Relative Point X:{relativePoint.X}");
			}
		}

		private void pictureBox_waveform_MouseUp(object? sender, MouseEventArgs e)
		{
			// Abort if any track is playing or if id is invalid
			if (AuH.Tracks.Any(track => track.WoE.PlaybackState == PlaybackState.Playing) || numericUpDown_id.Value < 1 || numericUpDown_id.Value > AuH.Tracks.Count)
			{
				return;
			}

			if (SeH.IsSelecting)
			{
				Point relativePoint = pictureBox_waveform.PointToClient(Cursor.Position);
				SeH.EndSelection(relativePoint);

				if (AuH.Tracks.Count > 0 && numericUpDown_id.Value > 0)
				{
					int id = (int) numericUpDown_id.Value - 1;
					int trackLength = AuH.Tracks[id].Floats.Length;

					// Zugriff auf die NumericUpDown-Werte:
					float samplesPerPixel = (float) numericUpDown_zoom.Value;
					float offset = OffsetTrack;

					// Korrekte Berechnung mit samplesPerPixel und Offset:
					int startSample = (int) (Math.Min(SeH.Start.X, SeH.End.X) * samplesPerPixel + offset);
					int endSample = (int) (Math.Max(SeH.Start.X, SeH.End.X) * samplesPerPixel + offset);

					// Bereichsbegrenzung (sehr wichtig!):
					startSample = Math.Max(0, Math.Min(trackLength - 1, startSample));
					endSample = Math.Max(0, Math.Min(trackLength - 1, endSample));

					// Sicherstellen, dass Start nicht größer ist als Ende und Werte im gültigen Bereich liegen.
					if (startSample <= endSample && endSample < trackLength)
					{
						float[] selectedFloats = AuH.Tracks[id].Floats[startSample..endSample];
						if (selectedFloats.Length > 0)
						{
							// Erstellen eines neuen Samples aus der Auswahl
							SampleObject newSample = new("Track - Ausschnitt " + AuH.Samples.Count, selectedFloats, AuH.Tracks[id].Sampletrate, AuH.Tracks[id].Bitdepth, AuH.Tracks[id].Channels);
							AuH.Samples.Add(newSample);

							UpdateSampleView();
							numericUpDown_idSample.Value = AuH.Samples.Count;

							Console.WriteLine($"Start X: {SeH.Start.X}, End X: {SeH.End.X}, SamplesPerPixel: {samplesPerPixel}, Offset: {offset}, TrackLength: {trackLength}");
							Console.WriteLine($"Berechnete Start Sample: {startSample}, Berechnete End Sample: {endSample}");
						}
					}
				}
			}
		}

		private void pictureBox_sample_MouseDown(object? sender, MouseEventArgs e)
		{
			// Abort if any sample is playing or if id is invalid
			if (AuH.Samples.Any(sample => sample.WoE.PlaybackState == PlaybackState.Playing) || numericUpDown_idSample.Value < 1 || numericUpDown_idSample.Value > AuH.Samples.Count)
			{
				return;
			}

			// If left mouse button and ctrl is pressed, do selection
			if (e.Button == MouseButtons.Left && ModifierKeys.HasFlag(Keys.Control))
			{
				Point relativePoint = pictureBox_sample.PointToClient(Cursor.Position);
				SeH.StartSelection(relativePoint, pictureBox_sample);
				Console.WriteLine($"MouseDown Relative Point X:{relativePoint.X}");
			}
		}

		private void pictureBox_sample_MouseMove(object? sender, MouseEventArgs e)
		{
			// Abort if any sample is playing or if id is invalid
			if (AuH.Samples.Any(sample => sample.WoE.PlaybackState == PlaybackState.Playing) || numericUpDown_idSample.Value < 1 || numericUpDown_idSample.Value > AuH.Samples.Count)
			{
				return;
			}

			if (SeH.IsSelecting)
			{
				Point relativePoint = pictureBox_sample.PointToClient(Cursor.Position);
				SeH.UpdateSelection(relativePoint);
				Console.WriteLine($"MouseMove Relative Point X:{relativePoint.X}");
			}
		}

		private void pictureBox_sample_MouseUp(object? sender, MouseEventArgs e)
		{
			// Abort if any sample is playing or if id is invalid
			if (AuH.Samples.Any(sample => sample.WoE.PlaybackState == PlaybackState.Playing) || numericUpDown_idSample.Value < 1 || numericUpDown_idSample.Value > AuH.Samples.Count)
			{
				return;
			}

			if (SeH.IsSelecting)
			{
				Point relativePoint = pictureBox_sample.PointToClient(Cursor.Position);
				SeH.EndSelection(relativePoint);

				if (AuH.Samples.Count > 0 && numericUpDown_idSample.Value > 0)
				{
					int id = (int) numericUpDown_idSample.Value - 1;
					int sampleLength = AuH.Samples[id].Floats.Length;

					// Zugriff auf die NumericUpDown-Werte:
					float samplesPerPixel = (float) numericUpDown_zoom.Value;
					float offset = OffsetSample;

					// Korrekte Berechnung mit samplesPerPixel und Offset:
					int startSample = (int) (Math.Min(SeH.Start.X, SeH.End.X) * samplesPerPixel + offset);
					int endSample = (int) (Math.Max(SeH.Start.X, SeH.End.X) * samplesPerPixel + offset);

					// Bereichsbegrenzung (sehr wichtig!):
					startSample = Math.Max(0, Math.Min(sampleLength - 1, startSample));
					endSample = Math.Max(0, Math.Min(sampleLength - 1, endSample));

					// Sicherstellen, dass Start nicht größer ist als Ende und Werte im gültigen Bereich liegen.
					if (startSample <= endSample && endSample < sampleLength)
					{
						float[] selectedFloats = AuH.Samples[id].Floats[startSample..endSample];
						if (selectedFloats.Length > 0)
						{
							// Erstellen eines neuen Samples aus der Auswahl
							SampleObject newSample = new("Sample - Ausschnitt " + AuH.Samples.Count, selectedFloats, AuH.Samples[id].Sampletrate, AuH.Samples[id].Bitdepth, AuH.Samples[id].Channels);
							AuH.Samples.Add(newSample);

							UpdateSampleView();
							numericUpDown_idSample.Value = AuH.Samples.Count;

							Console.WriteLine($"Start X: {SeH.Start.X}, End X: {SeH.End.X}, SamplesPerPixel: {samplesPerPixel}, Offset: {offset}, SampleLength: {sampleLength}");
							Console.WriteLine($"Berechnete Start Sample: {startSample}, Berechnete End Sample: {endSample}");
						}
					}
				}
			}
		}

		private void pictureBox_waveform_Paint(object? sender, PaintEventArgs e)
		{
			if (SeH.ActivePictureBox == pictureBox_waveform)
			{
				SeH.DrawSelection(e.Graphics);
			}
		}

		private void pictureBox_sample_Paint(object? sender, PaintEventArgs e)
		{
			if (SeH.ActivePictureBox == pictureBox_sample)
			{
				SeH.DrawSelection(e.Graphics);
			}
		}

		private void button_colorGraph_Click(object sender, EventArgs e)
		{
			// Color dialog
			ColorDialog cd = new();
			cd.Color = WfH.Graph;

			// Show dialog
			if (cd.ShowDialog() == DialogResult.OK)
			{
				// Set color
				WfH.Graph = cd.Color;

				// If color is dark. set Back to white, else to black
				if (WfH.Graph.GetBrightness() < 0.8)
				{
					WfH.Back = Color.White;
				}
				else
				{
					WfH.Back = Color.Black;
				}

				// Update track view
				UpdateTrackView();
			}
		}


		// ~~~~~ ~~~~~ ~~~~~ Playback speed, loop ~~~~~ ~~~~~ ~~~~~ \\

		private void numericUpDown_speed_ValueChanged(object sender, EventArgs e)
		{
			// Change speed of currently playing track
			foreach (var track in AuH.Tracks)
			{
				if (track.WoE.PlaybackState == PlaybackState.Playing)
				{

				}
			}
		}

		private void numericUpDown_speedSample_ValueChanged(object sender, EventArgs e)
		{
			// Change speed of currently playing track
			foreach (var sample in AuH.Samples)
			{
				if (sample.WoE.PlaybackState == PlaybackState.Playing)
				{

				}
			}
		}


		// ~~~~~ ~~~~~ ~~~~~ Export & delete ~~~~~ ~~~~~ ~~~~~ \\
		private void button_zipexport_Click(object sender, EventArgs e)
		{
			// Get track id
			int id = (int) numericUpDown_id.Value - 1;

			// Abort if id is invalid
			if (id < 0 || id >= AuH.Tracks.Count)
			{
				return;
			}

			// Get track
			string? trackName = AuH.Tracks[id].Name;
			trackName = Path.GetFileNameWithoutExtension(trackName);

			// Save file dialog for ZIP with Samples (add WaveHeader before saving)
			SaveFileDialog sfd = new();
			sfd.Title = @"Export samplepack ZIP";
			sfd.Filter = @"ZIP files|*.zip";
			sfd.FileName = @$"{trackName}_samplepack.zip";
			sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
			sfd.OverwritePrompt = true;

			// Save file dialog show
			if (sfd.ShowDialog() == DialogResult.OK)
			{
				// If file already exists, delete it
				while (File.Exists(sfd.FileName))
				{
					sfd.FileName += 1;
				}

				// Create ZIP
				using ZipArchive zip = ZipFile.Open(sfd.FileName, ZipArchiveMode.Create);

				// Add Samples
				foreach (SampleObject s in AuH.Samples)
				{
					// Create entry
					ZipArchiveEntry entry = zip.CreateEntry(s.Name + ".wav");

					// Write WaveHeader
					s.Bytes = AuH.AddRiffHeaderBytes(s.Bytes, s.Sampletrate, s.Bitdepth, s.Channels);

					// Write Bytes
					using Stream stream = entry.Open();
					stream.Write(s.Bytes, 0, s.Bytes.Length);
				}
			}
		}

		private void button_exportwav_Click(object sender, EventArgs e)
		{
			// Get id (0-based)
			int id = (int) numericUpDown_idSample.Value - 1;

			// Abort if id is invalid
			if (id < 0 || id >= AuH.Samples.Count)
			{
				return;
			}

			// Get selected SampleObject
			SampleObject selectedSample = AuH.Samples[id];

			// Save file dialog for WAV
			SaveFileDialog sfd = new();
			sfd.Title = "Export Sample as WAV";
			sfd.Filter = "WAV files|*.wav";
			sfd.FileName = selectedSample.Name + ".wav";
			sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
			sfd.OverwritePrompt = true;

			// Show save file dialog
			if (sfd.ShowDialog() == DialogResult.OK)
			{
				// Get bytes with RIFF header
				byte[] wavBytes = AuH.AddRiffHeaderBytes(selectedSample.Bytes, selectedSample.Sampletrate, selectedSample.Bitdepth, selectedSample.Channels);

				// Write bytes to file
				File.WriteAllBytes(sfd.FileName, wavBytes);
			}
		}

		private void button_toTracks_Click(object sender, EventArgs e)
		{
			// Move currently selected sample to Tracks: Append to Tracks & Update
			int id = (int) numericUpDown_idSample.Value - 1;

			// Abort if id is invalid
			if (id < 0 || id >= AuH.Samples.Count)
			{
				return;
			}

			// Add sample to tracks
			AuH.Tracks.Add(AuH.Samples[id]);

			// Remove sample from samples if CTRL is pressed
			if (ModifierKeys.HasFlag(Keys.Control))
			{
				AuH.Samples.RemoveAt(id);
			}

			// Update track view
			UpdateTrackView();
			UpdateSampleView();

			// Set value to last
			numericUpDown_id.Value = AuH.Tracks.Count;
		}


		private void button_deleteSample_Click(object sender, EventArgs e)
		{
			// Stop all other samples
			foreach (var sample in AuH.Samples)
			{
				if (sample.WoE.PlaybackState == PlaybackState.Playing)
				{
					sample.Stop();
				}
			}

			// Get id (0-based)
			int id = (int) numericUpDown_idSample.Value - 1;

			// Abort if id is invalid
			if (id < 0 || id >= AuH.Samples.Count)
			{
				return;
			}

			// If CTRL down, delete all samples
			if (ModifierKeys.HasFlag(Keys.Control))
			{
				AuH.Samples.Clear();
			}
			else
			{
				// Delete sample
				AuH.Samples.RemoveAt(id);
			}

			UpdateSampleView();
		}

		private void button_delete_Click(object sender, EventArgs e)
		{
			// Stop all other tracks
			foreach (var sample in AuH.Tracks)
			{
				if (sample.WoE.PlaybackState == PlaybackState.Playing)
				{
					sample.Stop();
				}
			}

			// Get id (0-based)
			int id = (int) numericUpDown_id.Value - 1;

			// Abort if id is invalid
			if (id < 0 || id >= AuH.Tracks.Count)
			{
				return;
			}

			// Delete track
			AuH.Tracks.RemoveAt(id);

			// Update track view
			UpdateTrackView();
		}


		// ~~~~~ ~~~~~ ~~~~~ Cut ~~~~~ ~~~~~ ~~~~~ \\
		private void comboBox_waveform_SelectedIndexChanged(object sender, EventArgs e)
		{
			// Refresh waveform
			UpdateTrackView();
			UpdateSampleView();
		}

		private void numericUpDown_lengthCut_ValueChanged(object sender, EventArgs e)
		{
			// Set min value of numericUpDown_maxLengthCut
			numericUpDown_maxLengthCut.Minimum = numericUpDown_lengthCut.Value;
		}

		private void numericUpDown_maxLengthCut_ValueChanged(object sender, EventArgs e)
		{
			// Set max value of numericUpDown_lengthCut
			numericUpDown_lengthCut.Maximum = numericUpDown_maxLengthCut.Value;
		}

		private void button_basicCut_Click(object sender, EventArgs e)
		{
			// Get id (0-based)
			int id = (int) numericUpDown_id.Value - 1;

			// Abort if id is invalid
			if (id < 0 || id >= AuH.Tracks.Count)
			{
				return;
			}

			// Call CuttingHandlings AutoCutBasic
			AuH.Samples.AddRange(CuH.AutoCutBasic5(AuH.Tracks[id], (float) numericUpDown_treshold.Value - 1, (int) numericUpDown_cutoff.Value, (int) numericUpDown_lengthCut.Value, (int) numericUpDown_silence.Value, (int) numericUpDown_fadein.Value, (int) numericUpDown_fadeout.Value, checkBox_normalize.Checked, (int) numericUpDown_maxLengthCut.Value, checkBox_nodoubles.Checked));

			// Merge samples to match min length
			AuH.Samples = CuH.MergeSamples(AuH.Samples, (int) numericUpDown_lengthCut.Value);

			// Update sample view
			UpdateSampleView();

			// Set id to last sample
			numericUpDown_idSample.Value = AuH.Samples.Count;
		}

		private void button_cutBeat_Click(object sender, EventArgs e)
		{
			// Get id (0-based)
			int id = (int) numericUpDown_id.Value - 1;

			// Abort if id is invalid
			if (id < 0 || id >= AuH.Tracks.Count)
			{
				return;
			}

			// Call CuttingHandlings AutoCutBeat
			AuH.Samples.AddRange(CuH.AutoCutBeat(AuH.Tracks[id], (float) numericUpDown_treshold.Value - 1, (int) numericUpDown_cutoff.Value, (int) numericUpDown_lengthCut.Value, (int) numericUpDown_fadein.Value, (int) numericUpDown_fadeout.Value, checkBox_normalize.Checked, checkBox_nodoubles.Checked));
			// Update sample view
			UpdateSampleView();

			// Set id to last sample
			numericUpDown_idSample.Value = AuH.Samples.Count;
		}




		// ~~~~~ ~~~~~ ~~~~~ Speed stretch ~~~~~ ~~~~~ ~~~~~ \\
		private void numericUpDown_startbpm_ValueChanged(object sender, EventArgs e)
		{
			// Adjust factor value
			numericUpDown_factorstretch.Value = numericUpDown_goalbpm.Value / numericUpDown_startbpm.Value;
		}

		private void numericUpDown_goalbpm_ValueChanged(object sender, EventArgs e)
		{
			// Adjust factor value
			numericUpDown_factorstretch.Value = numericUpDown_goalbpm.Value / numericUpDown_startbpm.Value;
		}

		private void numericUpDown_factorstretch_ValueChanged(object sender, EventArgs e)
		{
			// Adjust goalbpm value
			numericUpDown_goalbpm.Value = numericUpDown_startbpm.Value * numericUpDown_factorstretch.Value;
		}

		private void numericUpDown_stretchwindow_ValueChanged(object sender, EventArgs e)
		{
			// Get the current value
			int currentValue = (int) numericUpDown_stretchwindow.Value;

			// Check if the value has increased
			if (currentValue > lastWindowsize)
			{
				// Double the last value
				int newValue = lastWindowsize * 2;

				// Ensure the new value is within the allowed range
				if (newValue > 16384)
				{
					newValue = 16384;
				}

				numericUpDown_stretchwindow.Value = newValue;
			}
			else if (currentValue < lastWindowsize)
			{
				// Halve the last value
				int newValue = lastWindowsize / 2;

				// Ensure the new value is within the allowed range
				if (newValue < 64)
				{
					newValue = 64;
				}

				numericUpDown_stretchwindow.Value = newValue;
			}

			// Update the last value
			lastWindowsize = (int) numericUpDown_stretchwindow.Value;
		}






		private void button_stretchwsola_Click(object sender, EventArgs e)
		{
			// Get id (0-based)
			int id = (int) numericUpDown_id.Value - 1;

			// Abort if id is invalid
			if (id < 0 || id >= AuH.Tracks.Count)
			{
				return;
			}

			// Get selected SampleObject
			SampleObject selectedSample = AuH.Tracks[id];

			// Perform WSOLA stretching
			SampleObject? newSample = StH.StretchWsola(selectedSample, (float) numericUpDown_startbpm.Value, (float) numericUpDown_goalbpm.Value, (float) numericUpDown_factorstretch.Value, (int) numericUpDown_stretchwindow.Value, (int) numericUpDown_stretchhop.Value);

			// If not null, optionally normalize and add to samples
			if (newSample != null)
			{
				// Build name & rename
				string name = "Track [" + id + "]";
				name += $" - WSOLA(x{numericUpDown_factorstretch.Value})";
				if (checkBox_normalize.Checked)
				{
					name += " - Normalized";
				}
				newSample.Name = name;

				// Normalize if checked
				if (checkBox_normalize.Checked)
				{
					newSample.Normalize(null, true);
				}

				// Add to samples
				AuH.Samples.Add(newSample);
			}

			// Update sample view
			UpdateSampleView();

			// Set id to last sample
			numericUpDown_idSample.Value = AuH.Samples.Count;
		}

		private void button_stretch8bit_Click(object sender, EventArgs e)
		{
			// Get id (0-based)
			int id = (int) numericUpDown_id.Value - 1;

			// Abort if id is invalid
			if (id < 0 || id >= AuH.Tracks.Count)
			{
				return;
			}

			// Get selected SampleObject
			SampleObject selectedSample = AuH.Tracks[id];

			// Perform WSOLA stretching
			SampleObject? newSample = StH.Stretch8bit(selectedSample, (float) numericUpDown_startbpm.Value, (float) numericUpDown_goalbpm.Value, (float) numericUpDown_factorstretch.Value, (int) numericUpDown_stretchwindow.Value, (int) numericUpDown_stretchhop.Value);

			// If not null, optionally normalize and add to samples
			if (newSample != null)
			{
				// Build name & rename
				string name = "Track [" + id + "]";
				name += $" - 8BIT(x{numericUpDown_factorstretch.Value})";
				if (checkBox_normalize.Checked)
				{
					name += " - Normalized";
				}
				newSample.Name = name;

				// Normalize if checked
				if (checkBox_normalize.Checked)
				{
					newSample.Normalize(null, true);
				}

				// Add to samples
				AuH.Samples.Add(newSample);
			}



			// Update sample view
			UpdateSampleView();

			// Set id to last sample
			numericUpDown_idSample.Value = AuH.Samples.Count;
		}

		private void button_stretchvocoder_Click(object sender, EventArgs e)
		{
			// Get id (0-based)
			int id = (int) numericUpDown_id.Value - 1;

			// Abort if id is invalid
			if (id < 0 || id >= AuH.Tracks.Count)
			{
				return;
			}

			// Get selected SampleObject
			SampleObject selectedSample = AuH.Tracks[id];

			// Perform WSOLA stretching
			SampleObject? newSample = StH.StretchPhaseVocoder(selectedSample, (float) numericUpDown_startbpm.Value, (float) numericUpDown_goalbpm.Value, (float) numericUpDown_factorstretch.Value, (int) numericUpDown_stretchwindow.Value, (int) numericUpDown_stretchhop.Value);

			// If not null, optionally normalize and add to samples
			if (newSample != null)
			{
				// Build name & rename
				string name = "Track [" + id + "]";
				name += $" - VOCODER(x{numericUpDown_factorstretch.Value})";
				if (checkBox_normalize.Checked)
				{
					name += " - Normalized";
				}
				newSample.Name = name;

				// Normalize if checked
				if (checkBox_normalize.Checked)
				{
					newSample.Normalize(null, true);
				}

				// Add to samples
				AuH.Samples.Add(newSample);
			}

			// Update sample view
			UpdateSampleView();

			// Set id to last sample
			numericUpDown_idSample.Value = AuH.Samples.Count;
		}

		private void button_stretchquality_Click(object sender, EventArgs e)
		{
			// Get id (0-based)
			int id = (int) numericUpDown_id.Value - 1;

			// Abort if id is invalid
			if (id < 0 || id >= AuH.Tracks.Count)
			{
				return;
			}

			// Get selected SampleObject
			SampleObject selectedSample = AuH.Tracks[id];

			// Perform WSOLA stretching
			SampleObject? newSample = StH.StretchQuality(selectedSample, (float) numericUpDown_startbpm.Value, (float) numericUpDown_goalbpm.Value, (float) numericUpDown_factorstretch.Value, (int) numericUpDown_stretchwindow.Value, (int) numericUpDown_stretchhop.Value);

			// If not null, optionally normalize and add to samples
			if (newSample != null)
			{
				// Build name & rename
				string name = "Track [" + id + "]";
				name += $" - QUALITY(x{numericUpDown_factorstretch.Value})";
				if (checkBox_normalize.Checked)
				{
					name += " - Normalized";
				}
				newSample.Name = name;

				// Normalize if checked
				if (checkBox_normalize.Checked)
				{
					newSample.Normalize(null, true);
				}

				// Add to samples
				AuH.Samples.Add(newSample);
			}

			// Update sample view
			UpdateSampleView();

			// Set id to last sample
			numericUpDown_idSample.Value = AuH.Samples.Count;
		}

	}
}
