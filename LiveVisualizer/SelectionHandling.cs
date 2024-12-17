public class SelectionHandling
{
	public Point Start { get; set; }
	public Point End { get; set; }
	public bool IsSelecting { get; set; }
	public Color ColorSelection { get; set; } = Color.FromArgb(128, 72, 145, 220);
	private Pen selectionPen;
	public PictureBox PrimaryPictureBox { get; set; }
	public PictureBox SecondaryPictureBox { get; set; }
	public PictureBox? ActivePictureBox { get; set; }

	public SelectionHandling(PictureBox primaryPictureBox, PictureBox secondaryPictureBox)
	{
		PrimaryPictureBox = primaryPictureBox;
		SecondaryPictureBox = secondaryPictureBox;
		selectionPen = new Pen(ColorSelection, 1);
	}

	public void StartSelection(Point start, PictureBox? pictureBox)
	{
		Start = start;
		IsSelecting = true;
		ActivePictureBox = pictureBox;
	}

	public void UpdateSelection(Point end)
	{
		End = end;
		ActivePictureBox?.Invalidate();
	}

	public void EndSelection(Point end)
	{
		End = end;
		IsSelecting = false;
		ActivePictureBox?.Invalidate();
	}

	public void DrawSelection(Graphics g)
	{
		if (IsSelecting && ActivePictureBox != null)
		{
			Rectangle rect = new Rectangle(
				Math.Min(Start.X, End.X),
				0, // Start Y bei 0, um die volle Höhe zu nehmen
				Math.Abs(Start.X - End.X),
				ActivePictureBox.Height); // Höhe der PictureBox
			g.FillRectangle(new SolidBrush(ColorSelection), rect);
		}
	}


}