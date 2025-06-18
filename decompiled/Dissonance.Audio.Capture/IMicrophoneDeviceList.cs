using System.Collections.Generic;

namespace Dissonance.Audio.Capture;

public interface IMicrophoneDeviceList
{
	void GetDevices(List<string> output);
}
