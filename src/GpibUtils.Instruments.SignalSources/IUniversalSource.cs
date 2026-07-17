namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// A universal source (HP 3245A): a multi-channel precision DC voltage/current source and low-frequency
    /// waveform generator. Distinct from <see cref="ISignalSource"/> (RF CW power/frequency),
    /// <c>IDcVoltageCalibrator</c> and <c>IDcPowerSupply</c> — none of which express DC V <b>and</b> I output,
    /// autorange, a use-channel selector, and triggered/arbitrary output. Runs over any
    /// <see cref="Visa.IInstrumentSession"/>. New interface for issue #105 (P1 #89).
    /// </summary>
    public interface IUniversalSource
    {
        /// <summary>The resource string this source's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Identifies the instrument (the 3245A uses <c>ID?</c>, not <c>*IDN?</c>).</summary>
        string Identify();

        /// <summary>Device clear + reset (<c>RST</c>) to a known state.</summary>
        void Initialize();

        /// <summary>Resets the instrument (<c>RST</c>).</summary>
        void Reset();

        /// <summary>Selects the active output channel on two-channel units (<c>USE 0</c>/<c>USE 100</c>).</summary>
        void SelectChannel(UniversalSourceChannel channel);

        /// <summary>Sets a DC voltage output on the active channel (<c>APPLY DCV &lt;volts&gt;</c>).</summary>
        void SetDcVoltage(double volts);

        /// <summary>Sets a DC current output on the active channel (<c>APPLY DCI &lt;amps&gt;</c>).</summary>
        void SetDcCurrent(double amps);

        /// <summary>Enables or disables output autoranging (<c>ARANGE ON</c>/<c>ARANGE OFF</c>).</summary>
        void SetAutorange(bool on);

        /// <summary>Reads back the programmed output level of the active channel (<c>OUTPUT?</c>).</summary>
        double ReadOutput();
    }

    /// <summary>Output channel of a two-channel universal source (the <c>USE 0</c>/<c>USE 100</c> selectors).</summary>
    public enum UniversalSourceChannel { ChannelA, ChannelB }
}
