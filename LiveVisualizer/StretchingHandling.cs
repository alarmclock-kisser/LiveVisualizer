using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveVisualizer
{
	public class StretchingHandling
	{
		// ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ ATTRIBUTES ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ \\






		// ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ CONSTRUCTOR ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ \\






		// ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ METHODS ~~~~~ ~~~~~ ~~~~~ ~~~~~ ~~~~~ \\
		public SampleObject? StretchWsola(SampleObject sample, float startbpm = -1.0f, float goalbpm = -1.0f, float factor = -1.0f, int window = 1024, int hop = 2)
		{
			// If factor is not set, calculate it
			if (factor < 0)
			{
				// Abort if startbpm or goalbpm is not set
				if (startbpm > 0 && goalbpm > 0)
				{
					factor = goalbpm / startbpm;
				}
				else
				{
					return null;
				}
			}

			// Invert factor to ensure correct stretching
			factor = 1 / factor;

			// Get floats from sample
			float[] floats = sample.Floats;

			// Abort if floats is null
			if (floats.Length == 0)
			{
				return null;
			}

			// Calculate new length
			int newLength = (int) (floats.Length * factor);

			// Create new array
			float[] newFloats = new float[newLength];

			// Apply time-stretch algorithm (WSOLA - Waveform Similarity Overlap-Add)
			int windowSize = window; // Window size for WSOLA
			int hopSize = windowSize / hop; // Hop size for WSOLA
			int overlapSize = windowSize / 2; // Overlap size for WSOLA

			for (int i = 0; i < newLength; i += hopSize)
			{
				int originalIndex = (int) (i / factor);
				for (int j = 0; j < windowSize && i + j < newLength && originalIndex + j < floats.Length; j++)
				{
					newFloats[i + j] += floats[originalIndex + j] * (float) (0.5 * (1 - Math.Cos(2 * Math.PI * j / (windowSize - 1))));
				}
			}

			// Return new SampleObject with stretched audio
			return new SampleObject(sample.Name, newFloats, sample.Sampletrate, sample.Bitdepth, sample.Channels);
		}
	

		public SampleObject? Stretch8bit(SampleObject sample, float startbpm = -1.0f, float goalbpm = -1.0f, float factor = -1.0f, int window = 1024, int hop = 2)
		{
			// If factor is not set, calculate it
			if (factor < 0)
			{
				// Abort if startbpm or goalbpm is not set
				if (startbpm > 0 && goalbpm > 0)
				{
					factor = goalbpm / startbpm;
				}
				else
				{
					return null;
				}
			}

			// Invert factor to ensure correct stretching
			factor = 1 / factor;

			// Get floats from sample
			float[] floats = sample.Floats;

			// Abort if floats is null
			if (floats.Length == 0)
			{
				return null;
			}

			// Calculate new length
			int newLength = (int) (floats.Length * factor);

			// Create new array
			float[] newFloats = new float[newLength];

			// Apply time-stretch algorithm (WSOLA - Waveform Similarity Overlap-Add)
			int windowSize = window; // Window size for WSOLA
			int hopSize = windowSize / hop; // Hop size for WSOLA
			int overlapSize = windowSize / 2; // Overlap size for WSOLA

			for (int i = 0; i < newLength; i += hopSize)
			{
				int originalIndex = (int) (i / factor);
				for (int j = 0; j < windowSize && i + j < newLength && originalIndex + j < floats.Length; j++)
				{
					newFloats[i + j] += floats[originalIndex + j] * (float) Math.Cos(Math.PI * j / overlapSize);
				}
			}

			// Return new SampleObject with stretched audio
			return new SampleObject(sample.Name, newFloats, sample.Sampletrate, sample.Bitdepth, sample.Channels);
		}


		public SampleObject? StretchPhaseVocoder(SampleObject sample, float startbpm = -1.0f, float goalbpm = -1.0f, float factor = -1.0f, int window = 1024, int hop = 2)
		{
			// If factor is not set, calculate it
			if (factor < 0)
			{
				// Abort if startbpm or goalbpm is not set
				if (startbpm > 0 && goalbpm > 0)
				{
					factor = goalbpm / startbpm;
				}
				else
				{
					return null;
				}
			}

			// Invert factor to ensure correct stretching
			factor = 1 / factor;

			// Get floats from sample
			float[] floats = sample.Floats;

			// Abort if floats is null
			if (floats.Length == 0)
			{
				return null;
			}

			// Calculate new length
			int newLength = (int) (floats.Length * factor);

			// Create new array
			float[] newFloats = new float[newLength];

			// Apply time-stretch algorithm (Phase Vocoder)
			int windowSize = window; // Window size for Phase Vocoder
			int hopSize = windowSize / hop; // Hop size for Phase Vocoder
			float[] phase = new float[windowSize];
			float[] lastPhase = new float[windowSize];
			float[] magnitudes = new float[windowSize];
			float[] frequencies = new float[windowSize];

			for (int i = 0; i < newLength; i += hopSize)
			{
				int originalIndex = (int) (i / factor);
				for (int j = 0; j < windowSize && i + j < newLength && originalIndex + j < floats.Length; j++)
				{
					float real = floats[originalIndex + j] * (float) Math.Cos(phase[j]);
					float imag = floats[originalIndex + j] * (float) Math.Sin(phase[j]);
					magnitudes[j] = (float) Math.Sqrt(real * real + imag * imag);
					frequencies[j] = (float) Math.Atan2(imag, real);
					float phaseDiff = frequencies[j] - lastPhase[j];
					lastPhase[j] = frequencies[j];
					phase[j] += phaseDiff;
					newFloats[i + j] += magnitudes[j] * (float) Math.Cos(phase[j]);
				}
			}

			// Normalize the output to avoid clipping
			float maxAmplitude = newFloats.Max(Math.Abs);
			if (maxAmplitude > 0)
			{
				for (int i = 0; i < newFloats.Length; i++)
				{
					newFloats[i] /= maxAmplitude;
				}
			}

			// Return new SampleObject with stretched audio
			return new SampleObject(sample.Name, newFloats, sample.Sampletrate, sample.Bitdepth, sample.Channels);
		}

		public SampleObject? StretchQuality(SampleObject sample, float startbpm = -1.0f, float goalbpm = -1.0f, float factor = -1.0f, int window = 1024, int hop = 2)
		{
			// If factor is not set, calculate it
			if (factor < 0)
			{
				// Abort if startbpm or goalbpm is not set
				if (startbpm > 0 && goalbpm > 0)
				{
					factor = goalbpm / startbpm;
				}
				else
				{
					return null;
				}
			}

			// Invert factor to ensure correct stretching
			factor = 1 / factor;

			// Get floats from sample
			float[] floats = sample.Floats;

			// Abort if floats is null
			if (floats.Length == 0)
			{
				return null;
			}

			// Calculate new length
			int newLength = (int) (floats.Length * factor);

			// Create new array
			float[] newFloats = new float[newLength];

			// Apply Paulstretch algorithm
			int windowSize = window; // Window size for Paulstretch
			int hopSize = windowSize / hop; // Hop size for Paulstretch
			float[] windowFunction = new float[windowSize];
			for (int i = 0; i < windowSize; i++)
			{
				windowFunction[i] = (float) (0.5 * (1 - Math.Cos(2 * Math.PI * i / (windowSize - 1))));
			}

			float[] phase = new float[windowSize];
			float[] lastPhase = new float[windowSize];
			float[] magnitudes = new float[windowSize];
			float[] frequencies = new float[windowSize];

			for (int i = 0; i < newLength; i += hopSize)
			{
				int originalIndex = (int) (i / factor);
				for (int j = 0; j < windowSize && i + j < newLength && originalIndex + j < floats.Length; j++)
				{
					float real = floats[originalIndex + j] * windowFunction[j] * (float) Math.Cos(phase[j]);
					float imag = floats[originalIndex + j] * windowFunction[j] * (float) Math.Sin(phase[j]);
					magnitudes[j] = (float) Math.Sqrt(real * real + imag * imag);
					frequencies[j] = (float) Math.Atan2(imag, real);
					float phaseDiff = frequencies[j] - lastPhase[j];
					lastPhase[j] = frequencies[j];
					phase[j] += phaseDiff;
					newFloats[i + j] += magnitudes[j] * (float) Math.Cos(phase[j]);
				}
			}

			// Normalize the output to avoid clipping
			float maxAmplitude = newFloats.Max(Math.Abs);
			if (maxAmplitude > 0)
			{
				for (int i = 0; i < newFloats.Length; i++)
				{
					newFloats[i] /= maxAmplitude;
				}
			}

			// Return new SampleObject with stretched audio
			return new SampleObject(sample.Name, newFloats, sample.Sampletrate, sample.Bitdepth, sample.Channels);
		}


	}
}
