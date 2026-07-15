namespace GpibUtils.Instruments.Plotters
{
    /// <summary>
    /// An HP-GL pen plotter (HP 7090A / 7475A / 7550A). Streams HP-GL to draw, selects pens, moves the pen,
    /// writes labels, and (on auto-feed models) advances paper, over any <see cref="Visa.IInstrumentSession"/>
    /// — so the same driver runs on NI-VISA, a Prologix/AR488 adapter, or the in-memory simulator.
    /// </summary>
    public interface IPlotter
    {
        /// <summary>The resource string this plotter's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Reads the plotter identity (HP-GL <c>OI;</c> output-identification query).</summary>
        string Identify();

        /// <summary>Device clear + initialize to the default HP-GL state (<c>IN;</c>).</summary>
        void Initialize();

        /// <summary>Selects a pen by number (<c>SP n;</c>); pen 0 stores the pen (no pen).</summary>
        void SelectPen(int pen);

        /// <summary>Raises the pen (<c>PU;</c>).</summary>
        void PenUp();

        /// <summary>Lowers the pen (<c>PD;</c>).</summary>
        void PenDown();

        /// <summary>Moves the pen to an absolute position in plotter units (<c>PA x,y;</c>).</summary>
        void MoveTo(int x, int y);

        /// <summary>Writes a label at the current position (<c>LB&lt;text&gt;&lt;ETX&gt;</c>).</summary>
        void Label(string text);

        /// <summary>Streams a raw HP-GL document to the plotter (split into individual instructions).</summary>
        void PlotHpgl(string hpgl);

        /// <summary>True if the plotter can auto-load/advance paper (the 7550A); false for the 7090A/7475A.</summary>
        bool AutoFeed { get; }
    }
}
