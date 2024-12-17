using NAudio.Wave;
using System.Text;

namespace LiveVisualizer
{
	public class AudioHandling
	{
		// ~~~~~ ~~~~~ ~~~~~ Attributes ~~~~~ ~~~~~ ~~~~~ \\
		public List<SampleObject> Tracks = [];
		public List<SampleObject> Samples = [];
		public List<SampleObject> Clips = [];




		// ~~~~~ ~~~~~ ~~~~~ Constructor ~~~~~ ~~~~~ ~~~~~ \\
		public AudioHandling()
		{
			// Set Attributes

			// Register events

		}



		// ~~~~~ ~~~~~ ~~~~~ Methods ~~~~~ ~~~~~ ~~~~~ \\
		public SampleObject LoadSample(string path, string add = "")
		{
			// Load audio file
			AudioFileReader audioFile = new(path);

			// Get audio data
			int sampleCount = (int) (audioFile.Length / (audioFile.WaveFormat.BitsPerSample / 8));
			float[] floats = new float[sampleCount];
			int readSamples = audioFile.Read(floats, 0, sampleCount);

			// Get audio properties
			int samplerate = audioFile.WaveFormat.SampleRate;
			int bitrate = audioFile.WaveFormat.BitsPerSample;
			int channels = audioFile.WaveFormat.Channels;

			// Create SampleObject
			SampleObject sample = new(Path.GetFileName(path), floats, samplerate, bitrate, channels);

			// Add SampleObject to list
			if (!string.IsNullOrEmpty(add))
			{
				// If first letter is t, add to Tracks
				if (add[0].ToString().ToLower() == "c")
				{
					Clips.Add(sample);
				}
				else if (add[0].ToString().ToLower() == "t")
				{
					Tracks.Add(sample);
				}
				else if (add[0].ToString().ToLower() == "s")
				{
					Samples.Add(sample);
				}
			}

			// Return SampleObject
			return sample;
		}




		public string GetTimestamp(long offset = 0, bool isTrack = true)
		{
			string timestamp = "";

			// Get sample that is currently playing, if any
			List<SampleObject> samples = isTrack ? Tracks : Samples;
			foreach (SampleObject sample in samples)
			{
				if (sample.WoE.PlaybackState == PlaybackState.Playing || offset > 0)
				{
					long current = offset > 0 ? offset : sample.WoE.GetPosition();
					int bytesPerSample = (sample.Bitdepth / 8) * sample.Channels;
					long samplePosition = current / bytesPerSample;

					int hours = (int) (samplePosition / (sample.Sampletrate * 60 * 60));
					int minutes = (int) (samplePosition / (sample.Sampletrate * 60));
					int seconds = (int) (samplePosition / sample.Sampletrate % 60);
					int milliseconds = (int) ((samplePosition % sample.Sampletrate) * 1000 / sample.Sampletrate);

					timestamp = $"{hours:D1}:{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
				}
			}

			return timestamp;
		}





		public long[] GetCurrent()
		{
			long currentTrack = 0;
			long currentSample = 0;
			long currentClip = 0;

			// Get current position for tracks [0] and samples [1] and clip [2]
			foreach (SampleObject track in Tracks)
			{
				if (track.WoE.PlaybackState == PlaybackState.Playing)
				{
					currentTrack = track.WoE.GetPosition() / (track.Bitdepth / 8 * track.Channels);
				}
			}

			foreach (SampleObject sample in Samples)
			{
				if (sample.WoE.PlaybackState == PlaybackState.Playing)
				{
					currentSample = sample.WoE.GetPosition() / (sample.Bitdepth / 8 * sample.Channels);
				}
			}

			foreach (SampleObject clip in Clips)
			{
				if (clip.WoE.PlaybackState == PlaybackState.Playing)
				{
					currentClip = clip.WoE.GetPosition() / (clip.Bitdepth / 8 * clip.Channels);
				}
			}

			return [currentTrack, currentSample, currentClip];
		}


		public byte[] AddRiffHeaderBytes(byte[] bytes, int sampletrate, int bitdepth, int channels)
		{
			int byteRate = sampletrate * channels * (bitdepth / 8);
			int blockAlign = channels * (bitdepth / 8);
			int subChunk2Size = bytes.Length;
			int chunkSize = 36 + subChunk2Size;

			using MemoryStream memoryStream = new MemoryStream();
			using (BinaryWriter writer = new BinaryWriter(memoryStream))
			{
				// RIFF header
				writer.Write(Encoding.ASCII.GetBytes("RIFF"));
				writer.Write(chunkSize);
				writer.Write(Encoding.ASCII.GetBytes("WAVE"));

				// fmt sub-chunk
				writer.Write(Encoding.ASCII.GetBytes("fmt "));
				writer.Write(16); // SubChunk1Size for PCM
				writer.Write((short) 1); // AudioFormat (1 for PCM)
				writer.Write((short) channels);
				writer.Write(sampletrate);
				writer.Write(byteRate);
				writer.Write((short) blockAlign);
				writer.Write((short) bitdepth);

				// data sub-chunk
				writer.Write(Encoding.ASCII.GetBytes("data"));
				writer.Write(subChunk2Size);
				writer.Write(bytes);
			}

			return memoryStream.ToArray();
		}

	}


	public class SampleObject
	{
		// ~~~~~ ~~~~~ ~~~~~ Attributes ~~~~~ ~~~~~ ~~~~~ \\
		public string? Name;

		public byte[] Bytes;
		public float[] Floats;

		public int Sampletrate;
		public int Bitdepth;
		public int Channels;

		public int Length;

		public WaveOutEvent WoE;
		public bool doesLoop = false;

		// ~~~~~ ~~~~~ ~~~~~ Constructor ~~~~~ ~~~~~ ~~~~~ \\
		public SampleObject(string? name, float[] floats, int sampletrate, int bitdepth, int channels)
		{
			// Set Attributes
			Name = name;
			Floats = floats;

			Sampletrate = sampletrate;
			Bitdepth = bitdepth;
			Channels = channels;
			Length = Floats.Length;

			Bytes = GetBytes(bitdepth);

			WoE = new WaveOutEvent();
		}

		// ~~~~~ ~~~~~ ~~~~~ Methods ~~~~~ ~~~~~ ~~~~~ \\
		public byte[] GetBytes(int bitdepth)
		{
			int bytesPerSample = bitdepth / 8;
			byte[] bytes = new byte[Floats.Length * bytesPerSample];

			for (int i = 0; i < Floats.Length; i++)
			{
				byte[] byteArray;
				float sample = Floats[i];

				switch (bitdepth)
				{
					case 16:
						short shortSample = (short) (sample * short.MaxValue);
						byteArray = BitConverter.GetBytes(shortSample);
						break;
					case 24:
						int intSample24 = (int) (sample * (1 << 23));
						byteArray = new byte[3];
						byteArray[0] = (byte) (intSample24 & 0xFF);
						byteArray[1] = (byte) ((intSample24 >> 8) & 0xFF);
						byteArray[2] = (byte) ((intSample24 >> 16) & 0xFF);
						break;
					case 32:
						int intSample32 = (int) (sample * int.MaxValue);
						byteArray = BitConverter.GetBytes(intSample32);
						break;
					default:
						throw new ArgumentException("Unsupported bit depth");
				}

				Buffer.BlockCopy(byteArray, 0, bytes, i * bytesPerSample, bytesPerSample);
			}

			return bytes;
		}

		public void Play(int speedPercent = 100)
		{
			// New WaveOutEvent with Bytes
			if (Bytes.Length > 0)
			{
				// Get the speed factor from the speedPercent parameter
				float speedFactor = (float) speedPercent / 100;

				// Adjust the sample rate based on the speed factor
				int adjustedSampleRate = (int) (Sampletrate * speedFactor);

				var waveStream = new RawSourceWaveStream(new MemoryStream(Bytes), new WaveFormat(adjustedSampleRate, Bitdepth, Channels));

				if (WoE.PlaybackState == PlaybackState.Stopped)
				{
					WoE.Init(waveStream);
				}

				// Register events
				WoE.PlaybackStopped += OnPlaybackStopped;

				// Play
				WoE.Play();
			}
		}

		public void PlayWithLoop(int speedPercent = 100, bool loop = false)
		{
			// New WaveOutEvent with Bytes
			if (Bytes.Length > 0)
			{
				// Get the speed factor from the speedPercent parameter
				float speedFactor = (float) speedPercent / 100;

				// Adjust the sample rate based on the speed factor
				int adjustedSampleRate = (int) (Sampletrate * speedFactor);

				var waveStream = new RawSourceWaveStream(new MemoryStream(Bytes), new WaveFormat(adjustedSampleRate, Bitdepth, Channels));

				if (WoE.PlaybackState == PlaybackState.Stopped)
				{
					WoE.Init(waveStream);
				}

				// Register events
				WoE.PlaybackStopped += (sender, e) =>
				{
					if (loop && doesLoop)
					{
						PlayWithLoop(speedPercent, loop);
					}
					else
					{
						OnPlaybackStopped(sender, e);
					}
				};

				// Play
				WoE.Play();
			}
			else
			{
				return;
			}
		}


		private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
		{
			if (doesLoop)
			{
				PlayWithLoop(); // Restart playback if looping
			}
			else
			{
				// Reset position to start
				WoE = new WaveOutEvent();

				// Register events
				WoE.PlaybackStopped += OnPlaybackStopped;
			}
		}

		public void Stop()
		{
			doesLoop = false; // Ensure loop is disabled when stopping
			WoE.Stop();
			WoE.Dispose(); // Dispose to release resources and reset state
			WoE = new WaveOutEvent(); // Reinitialize WoE
			WoE.PlaybackStopped += OnPlaybackStopped; // Re-register event
		}

		public void Normalize(float[]? data, bool set = false)
		{
			// Get floats if not set
			if (data == null)
			{
				data = Floats;
			}

			float max = data.Max(Math.Abs);
			if (max > 0)
			{
				for (int i = 0; i < data.Length; i++)
				{
					data[i] /= max;
				}
			}

			if (set)
			{
				Floats = data;
				Bytes = GetBytes(Bitdepth);
			}
		}
	}


















}
