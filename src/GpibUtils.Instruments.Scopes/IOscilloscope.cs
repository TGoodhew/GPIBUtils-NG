namespace GpibUtils.Instruments.Scopes
{
    /// <summary>
    /// A digital oscilloscope (Rigol DS1054Z, …). Run/stop/single acquisition control, per-channel display,
    /// and simple automatic measurements, over any <see cref="Visa.IInstrumentSession"/>.
    /// </summary>
    public interface IOscilloscope
    {
        /// <summary>The resource string this scope's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Reads the instrument identity (<c>*IDN?</c>).</summary>
        string Identify();

        /// <summary>Device clear + a clean known state.</summary>
        void Initialize();

        /// <summary>Starts continuous acquisition (<c>:RUN</c>).</summary>
        void Run();

        /// <summary>Stops acquisition (<c>:STOP</c>).</summary>
        void Stop();

        /// <summary>Arms a single acquisition (<c>:SINGle</c>).</summary>
        void Single();

        /// <summary>Auto-scales the display (<c>:AUToscale</c>).</summary>
        void AutoScale();

        /// <summary>Turns a channel's display trace on or off.</summary>
        void SetChannelDisplay(int channel, bool on);

        /// <summary>Measures peak-to-peak voltage on a channel (volts).</summary>
        double MeasureVpp(int channel);
    }
}
