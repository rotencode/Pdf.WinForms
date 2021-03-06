﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Patagames.Pdf.Enums;
using Patagames.Pdf.Net.EventArguments;
using Patagames.Pdf.Net.Exceptions;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace Patagames.Pdf.Net.Controls.WinForms
{
	/// <summary>
	/// Represents a pdf view control for displaying an Pdf document.
	/// </summary>	
	public partial class PdfViewer : UserControl
	{
		#region Private fields
		private int _dpi = -1;
		private SelectInfo _selectInfo = new SelectInfo() { StartPage = -1 };
		private SortedDictionary<int, List<HighlightInfo>> _highlightedText = new SortedDictionary<int, List<HighlightInfo>>();
		private bool _mousePressed = false;
		private bool _mousePressedInLink = false;
		private bool _isShowSelection = false;
		private int _onstartPageIndex = 0;
		private Point _panToolInitialScrollPosition;
		private Point _panToolInitialMousePosition;

		private PdfForms _fillForms;
		private List<Rectangle> _selectedRectangles = new List<Rectangle>();
		private Pen _pageBorderColorPen;
		private Brush _selectColorBrush;
		private Pen _pageSeparatorColorPen;
		private Pen _currentPageHighlightColorPen;

		private PdfDocument _document;
		private SizeModes _sizeMode = SizeModes.FitToWidth;
		private Color _formHighlightColor;
		private Color _pageBackColor;
		private Color _pageBorderColor;
		private Color _textSelectColor;
		private Padding _pageMargin;
		private float _zoom;
		private ViewModes _viewMode;
		private Color _pageSeparatorColor;
		private bool _showPageSeparator;
		private Color _currentPageHighlightColor;
		private bool _showCurrentPageHighlight;
		private ContentAlignment _pageAlign;
		private RenderFlags _renderFlags = RenderFlags.FPDF_LCD_TEXT | RenderFlags.FPDF_NO_CATCH;
		private int _tilesCount;
		private MouseModes _mouseMode;
		private bool _showLoadingIcon = true;
		private bool _useProgressiveRender = true;
		private string _loadingIconText = Properties.Error.LoadingText;

		private RectangleF[] _renderRects;
		private int _startPage { get { return Document == null ? 0 : (ViewMode == ViewModes.SinglePage ? Document.Pages.CurrentIndex : 0); } }
		private int _endPage { get { return Document == null ? -1 : (ViewMode == ViewModes.SinglePage ? Document.Pages.CurrentIndex : (_renderRects != null ? _renderRects.Length - 1 : -1)); } }

		private PRCollection _prPages = new PRCollection();
		private Timer _invalidateTimer = null;
		private Font _loadingFont = new Font("Tahoma", 10);

		private bool _skipOnResize = false;
		private bool _loadedByViewer = true;

		private struct CaptureInfo
		{
			public PdfForms forms;
			public ISynchronizeInvoke sync;
			public Color color;
		}
		private CaptureInfo _externalDocCapture;
		#endregion

		#region Events
		/// <summary>
		/// Occurs whenever the Document property is changed.
		/// </summary>
		public event EventHandler AfterDocumentChanged;

		/// <summary>
		/// Occurs immediately before the document property would be changed.
		/// </summary>
		public event EventHandler<DocumentClosingEventArgs> BeforeDocumentChanged;

		/// <summary>
		/// Occurs whenever the document loads.
		/// </summary>
		public event EventHandler DocumentLoaded;

		/// <summary>
		/// Occurs before the document unloads.
		/// </summary>
		public event EventHandler<DocumentClosingEventArgs> DocumentClosing;

		/// <summary>
		/// Occurs whenever the document unloads.
		/// </summary>
		public event EventHandler DocumentClosed;

		/// <summary>
		/// Occurs when the <see cref="SizeMode"/> property has changed.
		/// </summary>
		public event EventHandler SizeModeChanged;

		/// <summary>
		/// Event raised when the value of the <see cref="PageBackColor"/> property is changed on Control..
		/// </summary>
		public event EventHandler PageBackColorChanged;

		/// <summary>
		/// Occurs when the <see cref="PageMargin"/> property has changed.
		/// </summary>
		public event EventHandler PageMarginChanged;

		/// <summary>
		/// Event raised when the value of the <see cref="PageBorderColor"/> property is changed on Control.
		/// </summary>
		public event EventHandler PageBorderColorChanged;

		/// <summary>
		/// Event raised when the value of the <see cref="TextSelectColor"/> property is changed on Control.
		/// </summary>
		public event EventHandler TextSelectColorChanged;

		/// <summary>
		/// Event raised when the value of the <see cref="FormHighlightColor"/> property is changed on Control.
		/// </summary>
		public event EventHandler FormHighlightColorChanged;

		/// <summary>
		/// Occurs when the <see cref="Zoom"/> property has changed.
		/// </summary>
		public event EventHandler ZoomChanged;

		/// <summary>
		/// Occurs when the current selection has changed.
		/// </summary>
		public event EventHandler SelectionChanged;

		/// <summary>
		/// Occurs when the <see cref="ViewMode"/> property has changed.
		/// </summary>
		public event EventHandler ViewModeChanged;

		/// <summary>
		/// Occurs when the <see cref="PageSeparatorColor"/> property has changed.
		/// </summary>
		public event EventHandler PageSeparatorColorChanged;

		/// <summary>
		/// Occurs when the <see cref="ShowPageSeparator"/> property has changed.
		/// </summary>
		public event EventHandler ShowPageSeparatorChanged;

		/// <summary>
		/// Occurs when the <see cref="CurrentPage"/> or <see cref="CurrentIndex"/> property has changed.
		/// </summary>
		public event EventHandler CurrentPageChanged;

		/// <summary>
		/// Occurs when the <see cref="CurrentPageHighlightColor"/> property has changed.
		/// </summary>
		public event EventHandler CurrentPageHighlightColorChanged;

		/// <summary>
		/// Occurs when the <see cref="ShowCurrentPageHighlight"/> property has changed.
		/// </summary>
		public event EventHandler ShowCurrentPageHighlightChanged;

		/// <summary>
		/// Occurs when the value of the <see cref="PageAlign"/> property has changed.
		/// </summary>
		public event EventHandler PageAlignChanged;

		/// <summary>
		/// Occurs before PdfLink or WebLink on the page was clicked.
		/// </summary>
		public event EventHandler<PdfBeforeLinkClickedEventArgs> BeforeLinkClicked;

		/// <summary>
		/// Occurs after PdfLink or WebLink on the page was clicked.
		/// </summary>
		public event EventHandler<PdfAfterLinkClickedEventArgs> AfterLinkClicked;

		/// <summary>
		/// Occurs when the value of the <see cref="RenderFlags"/> property has changed.
		/// </summary>
		public event EventHandler RenderFlagsChanged;

		/// <summary>
		/// Occurs when the value of the <see cref="TilesCount"/> property has changed.
		/// </summary>
		public event EventHandler TilesCountChanged;

		/// <summary>
		/// Occurs when the text highlighting changed
		/// </summary>
		public event EventHandler HighlightedTextChanged;

		/// <summary>
		/// Occurs when the value of the <see cref="MouseModes"/> property has changed.
		/// </summary>
		public event EventHandler MouseModeChanged;

		/// <summary>
		/// Occurs when the value of the <see cref="ShowLoadingIcon"/> property has changed.
		/// </summary>
		public event EventHandler ShowLoadingIconChanged;

		/// <summary>
		/// Occurs when the value of the <see cref="UseProgressiveRender"/> property has changed.
		/// </summary>
		public event EventHandler UseProgressiveRenderChanged;

		/// <summary>
		/// Occurs when the value of the <see cref="LoadingIconText"/> property has changed.
		/// </summary>
		public event EventHandler LoadingIconTextChanged;


		#endregion

		#region Event raises
		/// <summary>
		/// Raises the <see cref="AfterDocumentChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnAfterDocumentChanged(EventArgs e)
		{
			if (AfterDocumentChanged != null)
				AfterDocumentChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="BeforeDocumentChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		/// <returns>True if changing should be canceled, False otherwise</returns>
		protected virtual bool OnBeforeDocumentChanged(DocumentClosingEventArgs e)
		{
			if (BeforeDocumentChanged != null)
				BeforeDocumentChanged(this, e);
			return e.Cancel;
		}

		/// <summary>
		/// Raises the <see cref="DocumentLoaded"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnDocumentLoaded(EventArgs e)
		{
			if (DocumentLoaded != null)
				DocumentLoaded(this, e);
		}

		/// <summary>
		/// Raises the <see cref="DocumentClosing"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		/// <returns>True if closing should be canceled, False otherwise</returns>
		protected virtual bool OnDocumentClosing(DocumentClosingEventArgs e)
		{
			if (DocumentClosing != null)
				DocumentClosing(this, e);
			return e.Cancel;
		}
		
		/// <summary>
		/// Raises the <see cref="DocumentClosed"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnDocumentClosed(EventArgs e)
		{
			if (DocumentClosed != null)
				DocumentClosed(this, e);
		}

		/// <summary>
		/// Raises the <see cref="SizeModeChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnSizeModeChanged(EventArgs e)
		{
			if (SizeModeChanged != null)
				SizeModeChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="PageBackColorChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnPageBackColorChanged(EventArgs e)
		{
			if (PageBackColorChanged != null)
				PageBackColorChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="PageMarginChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnPageMarginChanged(EventArgs e)
		{
			if (PageMarginChanged != null)
				PageMarginChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="PageBorderColorChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnPageBorderColorChanged(EventArgs e)
		{
			if (PageBorderColorChanged != null)
				PageBorderColorChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="TextSelectColorChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnTextSelectColorChanged(EventArgs e)
		{
			if (TextSelectColorChanged != null)
				TextSelectColorChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="FormHighlightColorChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnFormHighlightColorChanged(EventArgs e)
		{
			if (FormHighlightColorChanged != null)
				FormHighlightColorChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="ZoomChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnZoomChanged(EventArgs e)
		{
			if (ZoomChanged != null)
				ZoomChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="SelectionChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnSelectionChanged(EventArgs e)
		{
			if (SelectionChanged != null)
				SelectionChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="ViewModeChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnViewModeChanged(EventArgs e)
		{
			if (ViewModeChanged != null)
				ViewModeChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="PageSeparatorColorChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnPageSeparatorColorChanged(EventArgs e)
		{
			if (PageSeparatorColorChanged != null)
				PageSeparatorColorChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="ShowPageSeparatorChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnShowPageSeparatorChanged(EventArgs e)
		{
			if (ShowPageSeparatorChanged != null)
				ShowPageSeparatorChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="CurrentPageChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnCurrentPageChanged(EventArgs e)
		{
			if (CurrentPageChanged != null)
				CurrentPageChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="CurrentPageHighlightColorChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnCurrentPageHighlightColorChanged(EventArgs e)
		{
			if (CurrentPageHighlightColorChanged != null)
				CurrentPageHighlightColorChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="ShowCurrentPageHighlightChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnShowCurrentPageHighlightChanged(EventArgs e)
		{
			if (ShowCurrentPageHighlightChanged != null)
				ShowCurrentPageHighlightChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="PageAlignChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnPageAlignChanged(EventArgs e)
		{
			if (PageAlignChanged != null)
				PageAlignChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="BeforeLinkClicked"/> event.
		/// </summary>
		/// <param name="e">An PdfBeforeLinkClickedEventArgs that contains the event data.</param>
		protected virtual void OnBeforeLinkClicked(PdfBeforeLinkClickedEventArgs e)
		{
			if (BeforeLinkClicked != null)
				BeforeLinkClicked(this, e);
		}

		/// <summary>
		/// Raises the <see cref="AfterLinkClicked"/> event.
		/// </summary>
		/// <param name="e">An PdfAfterLinkClickedEventArgs that contains the event data.</param>
		protected virtual void OnAfterLinkClicked(PdfAfterLinkClickedEventArgs e)
		{
			if (AfterLinkClicked != null)
				AfterLinkClicked(this, e);
		}

		/// <summary>
		/// Raises the <see cref="RenderFlagsChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnRenderFlagsChanged(EventArgs e)
		{
			if (RenderFlagsChanged != null)
				RenderFlagsChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="TilesCountChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnTilesCountChanged(EventArgs e)
		{
			if (TilesCountChanged != null)
				TilesCountChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="HighlightedTextChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnHighlightedTextChanged(EventArgs e)
		{
			if (HighlightedTextChanged != null)
				HighlightedTextChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="MouseModeChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnMouseModeChanged(EventArgs e)
		{
			if (MouseModeChanged != null)
				MouseModeChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="ShowLoadingIconChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnShowLoadingIconChanged(EventArgs e)
		{
			if (ShowLoadingIconChanged != null)
				ShowLoadingIconChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="UseProgressiveRenderChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnUseProgressiveRenderChanged(EventArgs e)
		{
			if (UseProgressiveRenderChanged != null)
				UseProgressiveRenderChanged(this, e);
		}

		/// <summary>
		/// Raises the <see cref="LoadingIconTextChanged"/> event.
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected virtual void OnLoadingIconTextChanged(EventArgs e)
		{
			if (LoadingIconTextChanged != null)
				LoadingIconTextChanged(this, e);
		}
		#endregion

		#region Public properties
		/// <summary>
		/// Gets or sets the Forms object associated with the current PdfViewer control.
		/// </summary>
		/// <remarks>The FillForms object are used for the correct processing of forms within the PdfViewer control</remarks>
		public PdfForms FillForms { get { return _fillForms; } }

		/// <summary>
		/// Gets or sets the PDF document associated with the current PdfViewer control.
		/// </summary>
		public PdfDocument Document
		{
			get
			{
				return _document;
			}
			set
			{
				if (_document != value)
				{
					if (OnBeforeDocumentChanged(new DocumentClosingEventArgs()))
						return;

					if (_document!= null && _loadedByViewer)
					{
						//we need to close the previous document if it was loaded by viewer
						if (OnDocumentClosing(new DocumentClosingEventArgs()))
							return; //the closing was canceled;
						_document.Dispose();
						_document = null;
						OnDocumentClosed(EventArgs.Empty);
					}
					else if (_document != null && !_loadedByViewer)
					{
						_document.Pages.CurrentPageChanged -= Pages_CurrentPageChanged;
						_document.Pages.PageInserted -= Pages_PageInserted;
						_document.Pages.PageDeleted -= Pages_PageDeleted;
						_document.Pages.ProgressiveRender -= Pages_ProgressiveRender;
					}
					SetScrollExtent(0, 0);
					_selectInfo = new SelectInfo() { StartPage = -1 };
					_highlightedText.Clear();
					_onstartPageIndex = 0;
					_renderRects = null;
					_loadedByViewer = false;
					Pdfium.FPDF_ShowSplash(true);
					ReleaseFillForms(_externalDocCapture);
					_document = value;
					UpdateLayout();
					if (_document != null)
					{
						if (_document.FormFill != _fillForms)
							_externalDocCapture = CaptureFillForms(_document.FormFill);
						_document.Pages.CurrentPageChanged += Pages_CurrentPageChanged;
						_document.Pages.PageInserted += Pages_PageInserted;
						_document.Pages.PageDeleted += Pages_PageDeleted;
						_document.Pages.ProgressiveRender += Pages_ProgressiveRender;
						SetCurrentPage(_onstartPageIndex);
						if (_document.Pages.Count > 0)
							ScrollToPage(_onstartPageIndex);
					}
					OnAfterDocumentChanged(EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Gets or sets the background color for the control under PDF page.
		/// </summary>
		public Color PageBackColor
		{
			get
			{
				return _pageBackColor;
			}
			set
			{
				if (_pageBackColor != value)
				{
					_pageBackColor = value;
					Invalidate();
					OnPageBackColorChanged(EventArgs.Empty);
				}

			}
		}

		/// <summary>
		/// Specifies space between pages margins
		/// </summary>
		public Padding PageMargin
		{
			get
			{
				return _pageMargin;
			}
			set
			{
				if (_pageMargin != value)
				{
					_pageMargin = value;
					UpdateLayout();
					OnPageMarginChanged(EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Gets or sets padding within the control.
		/// </summary>
		/// <value>A System.Windows.Forms.Padding representing the control's internal spacing characteristics.</value>
		public new Padding Padding
		{
			get
			{
				return base.Padding;
			}
			set
			{
				if (base.Padding != value)
				{
					base.Padding = value;
					UpdateLayout();
				}
			}
		}

		/// <summary>
		/// Gets or sets the border color of the page
		/// </summary>
		public Color PageBorderColor
		{
			get
			{
				return _pageBorderColor;
			}
			set
			{
				if (_pageBorderColor != value)
				{
					_pageBorderColor = value;
					if (_pageBorderColorPen != null)
						_pageBorderColorPen.Dispose();
					_pageBorderColorPen = new Pen(_pageBorderColor);
					Invalidate();
					OnPageBorderColorChanged(EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Control how the PdfViewer will handle  pages placement and control sizing
		/// </summary>
		public SizeModes SizeMode
		{
			get
			{
				return _sizeMode;
			}
			set
			{
				if (_sizeMode != value)
				{
					_sizeMode = value;
					UpdateLayout();
					OnSizeModeChanged(EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Gets or sets the selection color of the control.
		/// </summary>
		public Color TextSelectColor
		{
			get
			{
				return _textSelectColor;
			}
			set
			{
				if (_textSelectColor != value)
				{
					_textSelectColor = value;
					if (_selectColorBrush != null)
						_selectColorBrush.Dispose();
					_selectColorBrush = new SolidBrush(_textSelectColor);
					Invalidate();
					OnTextSelectColorChanged(EventArgs.Empty);
				}

			}
		}

		/// <summary>
		/// Gets or set the highlight color of the form fields in the document.
		/// </summary>
		public Color FormHighlightColor
		{
			get
			{
				return _formHighlightColor;
			}
			set
			{
				if (_formHighlightColor != value)
				{
					_formHighlightColor = value;
					if (_fillForms != null)
						_fillForms.SetHighlightColor(FormFieldTypes.FPDF_FORMFIELD_UNKNOWN, _formHighlightColor);
					if(Document!= null && !_loadedByViewer && _externalDocCapture.forms!= null)
						_externalDocCapture.forms.SetHighlightColor(FormFieldTypes.FPDF_FORMFIELD_UNKNOWN, _formHighlightColor);
					Invalidate();
					OnFormHighlightColorChanged(EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// This property allows you to scale the PDF page. To take effect the <see cref="SizeMode"/> property should be Zoom
		/// </summary>
		public float Zoom
		{
			get
			{
				return _zoom;
			}
			set
			{
				if (_zoom != value)
				{
					_zoom = value;
					UpdateLayout();
					OnZoomChanged(EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Gets selected text from PdfView control
		/// </summary>
		public string SelectedText
		{
			get
			{
				if (Document == null)
					return "";

				var selTmp = NormalizeSelectionInfo();

				if (selTmp.StartPage < 0 || selTmp.StartIndex < 0)
					return "";

				string ret = "";
				for (int i = selTmp.StartPage; i <= selTmp.EndPage; i++)
				{
					if (ret != "")
						ret += "\r\n";

					int s = 0;
					if (i == selTmp.StartPage)
						s = selTmp.StartIndex;

					int len = Document.Pages[i].Text.CountChars;
					if (i == selTmp.EndPage)
						len = (selTmp.EndIndex + 1) - s;

					ret += Document.Pages[i].Text.GetText(s, len);
				}
				return ret;
			}
		}

		/// <summary>
		/// Gets information about selected text in a PdfView control
		/// </summary>
		public SelectInfo SelectInfo { get { return NormalizeSelectionInfo(); } }

		/// <summary>
		/// Control how the PdfViewer will display pages
		/// </summary>
		public ViewModes ViewMode
		{
			get
			{
				return _viewMode;
			}
			set
			{
				if (_viewMode != value)
				{
					_viewMode = value;
					UpdateLayout();
					OnViewModeChanged(EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Gets or sets the page separator color.
		/// </summary>
		public Color PageSeparatorColor
		{
			get
			{
				return _pageSeparatorColor;
			}
			set
			{
				if (_pageSeparatorColor != value)
				{
					_pageSeparatorColor = value;
					if (_pageSeparatorColorPen != null)
						_pageSeparatorColorPen.Dispose();
					_pageSeparatorColorPen = new Pen(_pageSeparatorColor);
					Invalidate();
					OnPageSeparatorColorChanged(EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Determines whether the page separator is visible or hidden
		/// </summary>
		public bool ShowPageSeparator
		{
			get
			{
				return _showPageSeparator;
			}
			set
			{
				if (_showPageSeparator != value)
				{
					_showPageSeparator = value;
					Invalidate();
					OnShowPageSeparatorChanged(EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Gets or sets the current page highlight color.
		/// </summary>
		public Color CurrentPageHighlightColor
		{
			get
			{
				return _currentPageHighlightColor;
			}
			set
			{
				if (_currentPageHighlightColor != value)
				{
					_currentPageHighlightColor = value;
					if (_currentPageHighlightColorPen != null)
						_currentPageHighlightColorPen.Dispose();
					_currentPageHighlightColorPen = new Pen(_currentPageHighlightColor, 4);
					Invalidate();
					OnCurrentPageHighlightColorChanged(EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Determines whether the current page's highlight is visible or hidden.
		/// </summary>
		public bool ShowCurrentPageHighlight
		{
			get
			{
				return _showCurrentPageHighlight;
			}
			set
			{
				if (_showCurrentPageHighlight != value)
				{
					_showCurrentPageHighlight = value;
					Invalidate();
					OnShowCurrentPageHighlightChanged(EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Gets or sets the current index of a page in PdfPageCollection
		/// </summary>
		public int CurrentIndex
		{
			get
			{
				if (Document == null)
					return -1;
				return Document.Pages.CurrentIndex;
			}
			set
			{
				if (Document == null)
					return;
				Document.Pages.CurrentIndex = value;
			}
		}

		/// <summary>
		/// Gets the current PdfPage item by <see cref="CurrentIndex "/>
		/// </summary>
		public PdfPage CurrentPage { get { return Document.Pages.CurrentPage; } }

		/// <summary>
		/// Gets or sets a value indicating whether the control can accept PDF document through Document property.
		/// </summary>
		[Obsolete("This property is ignored now", false)]
		public bool AllowSetDocument { get; set; }

		/// <summary>
		/// Gets or sets the alignment of page in the control.
		/// </summary>
		public ContentAlignment PageAlign
		{
			get
			{
				return _pageAlign;
			}
			set
			{
				if (_pageAlign != value)
				{
					_pageAlign = value;
					UpdateLayout();
					OnPageAlignChanged(EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Gets or sets a RenderFlags. None for normal display, or combination of <see cref="RenderFlags"/>
		/// </summary>
		public RenderFlags RenderFlags
		{
			get
			{
				return _renderFlags;
			}
			set
			{
				if (_renderFlags != value)
				{
					_renderFlags = value;
					Invalidate();
					OnRenderFlagsChanged(EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Gets or sets visible page count for tiles view mode
		/// </summary>
		public int TilesCount
		{
			get
			{
				return _tilesCount;
			}
			set
			{
				int tmp = value < 2 ? 2 : value;
				if (_tilesCount != tmp)
				{
					_tilesCount = tmp;
					UpdateLayout();
					OnTilesCountChanged(EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Gets information about highlighted text in a PdfView control
		/// </summary>
		public SortedDictionary<int, List<HighlightInfo>> HighlightedTextInfo { get { return _highlightedText; } }

		/// <summary>
		/// Gets or sets mouse mode for pdf viewer control
		/// </summary>
		public MouseModes MouseMode
		{
			get
			{
				return _mouseMode;
			}
			set
			{
				if (_mouseMode != value)
				{
					_mouseMode = value;
					OnMouseModeChanged(EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Determines whether the page's loading icon should be shown
		/// </summary>
		public bool ShowLoadingIcon
		{
			get
			{
				return _showLoadingIcon;
			}
			set
			{
				if (_showLoadingIcon != value)
				{
					_showLoadingIcon = value;
					OnShowLoadingIconChanged(EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// If true the progressive rendering is used for render page
		/// </summary>
		public bool UseProgressiveRender
		{
			get
			{
				return _useProgressiveRender;
			}
			set
			{
				if (_useProgressiveRender != value)
				{
					UpdateLayout();
					_useProgressiveRender = value;
					OnUseProgressiveRenderChanged(EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Gets or sets loading icon text in progressive rendering mode
		/// </summary>
		public string LoadingIconText
		{
			get
			{
				return _loadingIconText;
			}
			set
			{
				if (_loadingIconText != value)
				{
					_loadingIconText = value;
					OnLoadingIconTextChanged(EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether the PdfViewer will dispose any pages placed outside of its visible boundaries.
		/// </summary>
		public bool PageAutoDispose { get; set; }
		#endregion

		#region Public methods
		/// <summary>
		/// Scrolls the control view to the specified page.
		/// </summary>
		/// <param name="index">Zero-based index of a page.</param>
		public void ScrollToPage(int index)
		{
			if (Document == null)
				return;
			if (Document.Pages.Count == 0)
				return;
			if (index < 0 || index > Document.Pages.Count - 1)
				return;

			if (ViewMode == ViewModes.SinglePage)
			{
				if (index != CurrentIndex)
				{
					SetCurrentPage(index);
					_prPages.ReleaseCanvas();
				}
				Invalidate();
			}
			else
			{
				var rect = RFTR(_renderRects[index]);
                SetScrollPos(rect.X, rect.Y);
			}
		}

		/// <summary>
		/// Scrolls the control view to the specified character on the current page
		/// </summary>
		/// <param name="charIndex">Character index</param>
		public void ScrollToChar(int charIndex)
		{
			ScrollToChar(CurrentIndex, charIndex);
		}

		/// <summary>
		/// Scrolls the control view to the specified character on the specified page
		/// </summary>
		/// <param name="charIndex">Character index</param>
		/// <param name="pageIndex">Zero-based index of a page.</param>
		public void ScrollToChar(int pageIndex, int charIndex)
		{
			if (Document == null)
				return;
			if (Document.Pages.Count == 0)
				return;
			if (pageIndex < 0)
				return;
			var page = Document.Pages[pageIndex];
			int cnt = page.Text.CountChars;
			if (charIndex < 0)
				charIndex = 0;
			if (charIndex >= cnt)
				charIndex = cnt - 1;
			if (charIndex < 0)
				return;
			var ti = page.Text.GetTextInfo(charIndex, 1);
			if (ti.Rects == null || ti.Rects.Count == 0)
				return;

			ScrollToPage(pageIndex);
			var pt = PageToClient(pageIndex, new PointF(ti.Rects[0].left, ti.Rects[0].top));
			var curPt = AutoScrollPosition;
            SetScrollPos(pt.X - curPt.X, pt.Y - curPt.Y);
		}

		/// <summary>
		/// Scrolls the control view to the specified point on the specified page
		/// </summary>
		/// <param name="pageIndex">Zero-based index of a page.</param>
		/// <param name="pagePoint">Point on the page in the page's coordinate system</param>
		public void ScrollToPoint(int pageIndex, PointF pagePoint)
		{
			if (Document == null)
				return;
			int count = Document.Pages.Count;
			if (count == 0)
				return;
			if (pageIndex < 0 || pageIndex > count - 1)
				return;

            ScrollToPage(pageIndex);
            var pt = PageToClient(pageIndex, pagePoint);
			var curPt = AutoScrollPosition;
            SetScrollPos(pt.X - curPt.X, pt.Y - curPt.Y);
		}

		/// <summary>
		/// Rotates the specified page to the specified angle.
		/// </summary>
		/// <param name="pageIndex">Zero-based index of a page for rotation.</param>
		/// <param name="angle">The angle which must be turned page</param>
		/// <remarks>The PDF page rotates clockwise. See <see cref="PageRotate"/> for details.</remarks>
		public void RotatePage(int pageIndex, PageRotate angle)
		{
			if (Document == null)
				return;
			Document.Pages[pageIndex].Rotation = angle;
			UpdateLayout();

		}

		/// <summary>
		/// Selects the text contained in specified pages.
		/// </summary>
		/// <param name="SelInfo"><see cref="SelectInfo"/> structure that describe text selection parameters.</param>
		public void SelectText(SelectInfo SelInfo)
		{
			SelectText(SelInfo.StartPage, SelInfo.StartIndex, SelInfo.EndPage, SelInfo.EndIndex);
		}

		/// <summary>
		/// Selects the text contained in specified pages.
		/// </summary>
		/// <param name="startPage">Zero-based index of a starting page.</param>
		/// <param name="startIndex">Zero-based char index on a startPage.</param>
		/// <param name="endPage">Zero-based index of a ending page.</param>
		/// <param name="endIndex">Zero-based char index on a endPage.</param>
		public void SelectText(int startPage, int startIndex, int endPage, int endIndex)
		{
			if (Document == null)
				return;

			if (startPage > Document.Pages.Count - 1)
				startPage = Document.Pages.Count - 1;
			if (startPage < 0)
				startPage = 0;

			if (endPage > Document.Pages.Count - 1)
				endPage = Document.Pages.Count - 1;
			if (endPage < 0)
				endPage = 0;

			int startCnt = Document.Pages[startPage].Text.CountChars;
			int endCnt = Document.Pages[endPage].Text.CountChars;

			if (startIndex > startCnt - 1)
				startIndex = startCnt - 1;
			if (startIndex < 0)
				startIndex = 0;

			if (endIndex > endCnt - 1)
				endIndex = endCnt - 1;
			if (endIndex < 0)
				endIndex = 0;

			_selectInfo = new SelectInfo()
			{
				StartPage = startPage,
				StartIndex = startIndex,
				EndPage = endPage,
				EndIndex = endIndex,
			};
			_isShowSelection = true;
			Invalidate();
			OnSelectionChanged(EventArgs.Empty);
		}

		/// <summary>
		/// Clear text selection
		/// </summary>
		public void DeselectText()
		{
			_selectInfo = new SelectInfo()
			{
				StartPage = -1,
			};
			Invalidate();
			OnSelectionChanged(EventArgs.Empty);
		}

		/// <summary>
		/// Determines if the specified point is contained within Pdf page.
		/// </summary>
		/// <param name="pt">The System.Drawing.Point to test.</param>
		/// <returns>
		/// This method returns the zero based page index if the point represented by pt is contained within this page; otherwise -1.
		/// </returns>
		public int PointInPage(Point pt)
		{
			for (int i = _startPage; i <= _endPage; i++)
			{
				//Actual coordinates of the page with the scroll
				Rectangle actualRect = CalcActualRect(i);
				if (actualRect.Contains(pt))
					return i;
			}
			return -1;
		}

		/// <summary>
		/// Computes the location of the specified client point into page coordinates.
		/// </summary>
		/// <param name="pageIndex">Page index. Can be obtained by <see cref="PointInPage"/> method.</param>
		/// <param name="pt">The client coordinate Point to convert. </param>
		/// <returns>A Point that represents the converted Point, pt, in page coordinates.</returns>
		/// <exception cref="IndexOutOfRangeException">The page index is out of range</exception>
		/// <remarks>Permitted range of pages depends on the current view type and on some other parameters in the control.</remarks>
		public PointF ClientToPage(int pageIndex, Point pt)
		{
			if (pageIndex < _startPage || pageIndex > _endPage)
				throw new IndexOutOfRangeException(Properties.Error.err0002);
			var page = Document.Pages[pageIndex];
			var ar = CalcActualRect(pageIndex);
			return page.DeviceToPage(ar.X, ar.Y, ar.Width, ar.Height, PageRotation(page), pt.X, pt.Y);
		}

		/// <summary>
		/// Computes the location of the specified page point into client coordinates.
		/// </summary>
		/// <param name="pageIndex">Page index. Can be obtained by <see cref="PointInPage"/> method.</param>
		/// <param name="pt">The page coordinate Point to convert. </param>
		/// <returns>A Point that represents the converted Point, pt, in client coordinates.</returns>
		/// <exception cref="IndexOutOfRangeException">The page index is out of range</exception>
		/// <remarks>Permitted range of pages depends on the current view type and on some other parameters in the control.</remarks>
		public Point PageToClient(int pageIndex, PointF pt)
		{
			if(pageIndex < _startPage || pageIndex > _endPage)
				throw new IndexOutOfRangeException(Properties.Error.err0002);
			var page = Document.Pages[pageIndex];
			var ar = CalcActualRect(pageIndex);
			return page.PageToDevice(ar.X, ar.Y, ar.Width, ar.Height, PageRotation(page), pt.X, pt.Y);
		}


		/// <summary>
		/// Highlight text on the page
		/// </summary>
		/// <param name="pageIndex">Zero-based index of the page</param>
		/// <param name="highlightInfo">Sets the options for highlighting text</param>
		public void HighlightText(int pageIndex, HighlightInfo highlightInfo)
		{
			HighlightText(pageIndex, highlightInfo.CharIndex, highlightInfo.CharsCount, highlightInfo.Color);
		}

		/// <summary>
		/// Highlight text on the page
		/// </summary>
		/// <param name="pageIndex">Zero-based index of the page</param>
		/// <param name="charIndex">Zero-based char index on the page.</param>
		/// <param name="charsCount">The number of highlighted characters on the page or -1 for highlight text from charIndex to end of the page.</param>
		/// <param name="color">Highlight color</param>
		public void HighlightText(int pageIndex, int charIndex, int charsCount, Color color)
		{
			//normalize all user input
			if (pageIndex < 0)
				pageIndex = 0;
			if (pageIndex > Document.Pages.Count - 1)
				pageIndex = Document.Pages.Count - 1;

			int charsCnt = Document.Pages[pageIndex].Text.CountChars;
			if (charIndex < 0)
				charIndex = 0;
			if (charIndex > charsCnt - 1)
				charIndex = charsCnt - 1;
			if (charsCount < 0)
				charsCount = charsCnt - charIndex;
			if (charIndex + charsCount > charsCnt )
				charsCount = charsCnt - 1 - charIndex;
			if (charsCount <= 0)
				return;

			var newEntry = new HighlightInfo() { CharIndex = charIndex, CharsCount = charsCount, Color = color };

			if (!_highlightedText.ContainsKey(pageIndex))
			{
				if (color != Color.Empty)
				{
					_highlightedText.Add(pageIndex, new List<HighlightInfo>());
					_highlightedText[pageIndex].Add(newEntry);
				}
			}
			else
			{
				var entries = _highlightedText[pageIndex];
				//Analize exists entries and remove overlapped and trancate intersecting entries
				for (int i = entries.Count - 1; i >= 0; i--)
				{
					List<HighlightInfo> calcEntries;
					if (CalcIntersectEntries(entries[i], newEntry, out calcEntries))
					{
						if (calcEntries.Count == 0)
							entries.RemoveAt(i);
						else
							for (int j = 0; j < calcEntries.Count; j++)
								if (j == 0)
									entries[i] = calcEntries[j];
								else
									entries.Insert(i, calcEntries[j]);
					}
				}
				if (color != Color.Empty)
					entries.Add(newEntry);
			}

			Invalidate();
			OnHighlightedTextChanged(EventArgs.Empty);
		}

		/// <summary>
		/// Removes highlight from the text
		/// </summary>
		public void RemoveHighlightFromText()
		{
			_highlightedText.Clear();
			Invalidate();
		}

		/// <summary>
		/// Removes highlight from the text
		/// </summary>
		/// <param name="pageIndex">Zero-based index of the page</param>
		/// <param name="charIndex">Zero-based char index on the page.</param>
		/// <param name="charsCount">The number of highlighted characters on the page or -1 for highlight text from charIndex to end of the page.</param>
		public void RemoveHighlightFromText(int pageIndex, int charIndex, int charsCount)
		{
			HighlightText(pageIndex, charIndex, charsCount, Color.Empty);
		}

		/// <summary>
		/// Highlight selected text on the page by specified color
		/// </summary>
		/// <param name="color">Highlight color</param>
		public void HilightSelectedText(Color color)
		{
			var selInfo = SelectInfo;
			if (selInfo.StartPage < 0 || selInfo.StartIndex < 0)
				return;

			for (int i = selInfo.StartPage; i <= selInfo.EndPage; i++)
			{
				int start = (i == selInfo.StartPage ? selInfo.StartIndex : 0);
				int len = (i == selInfo.EndPage ? (selInfo.EndIndex+1) - start : -1);
				HighlightText(i, start, len, color);
			}
		}

		/// <summary>
		/// Removes highlight from selected text
		/// </summary>
		public void RemoveHilightFromSelectedText()
		{
			HilightSelectedText(Color.Empty);
		}

		/// <summary>
		/// Ensures that all sizes and positions of pages of a PdfViewer control are properly updated for layout.
		/// </summary>
		public void UpdateLayout()
		{
			_prPages.ReleaseCanvas(); //something changed. Release canvas

			if (Document == null || Document.Pages.Count <= 0 || Width==0 || Height==0)
			{
				_renderRects = null;
				Invalidate();
				return;
			}

			var pagePoint = new PointF(0, 0);
			bool needToScroll = false;
			if (_renderRects != null)
			{
				pagePoint = ClientToPage(CurrentIndex, new Point(0, 0));
				needToScroll = true;
			}

			SizeF size;
			switch (ViewMode)
			{
				case ViewModes.Vertical:
					size = CalcVertical();
					break;
				case ViewModes.Horizontal:
					size = CalcHorizontal();
					break;
				case ViewModes.TilesVertical:
					size = CalcTilesVertical();
					break;
				default:
					size = CalcSingle();
					AdjustFormScrollbars(true); //It's need to prevent the bug with scrollbar
					break;
			}

			if (size.Width != 0 && size.Height != 0)
			{
				_skipOnResize = true;
				SetScrollExtent((int)size.Width, (int)size.Height);
				_skipOnResize = false;
				AdjustFormScrollbars(true);
                Invalidate();
			}
			if(needToScroll)
				ScrollToPoint(CurrentIndex, pagePoint);
		}
       
		/// <summary>
		/// Clear internal render buffer for rerender pages in Progressive mode
		/// </summary>
		public void ClearRenderBuffer()
		{
			_prPages.ReleaseCanvas();
		}

		/// <summary>
		/// Calculates the actual rectangle of the specified page in client coordinates
		/// </summary>
		/// <param name="index">Zero-based page index</param>
		/// <returns>Calculated rectangle</returns>
		public Rectangle CalcActualRect(int index)
		{
			var rect = RFTR(_renderRects[index]);
			rect.X += AutoScrollPosition.X;
			rect.Y += AutoScrollPosition.Y;
			return rect;
		}

		#endregion

		#region Load and Close document
		/// <summary>
		/// Open and load a PDF document from a file.
		/// </summary>
		/// <param name="path">Path to the PDF file (including extension)</param>
		/// <param name="password">A string used as the password for PDF file. If no password needed, empty or NULL can be used.</param>
		/// <exception cref="Exceptions.UnknownErrorException">unknown error</exception>
		/// <exception cref="Exceptions.PdfFileNotFoundException">file not found or could not be opened</exception>
		/// <exception cref="Exceptions.BadFormatException">file not in PDF format or corrupted</exception>
		/// <exception cref="Exceptions.InvalidPasswordException">password required or incorrect password</exception>
		/// <exception cref="Exceptions.UnsupportedSecuritySchemeException">unsupported security scheme</exception>
		/// <exception cref="Exceptions.PdfiumException">Error occured in PDFium. See ErrorCode for detail</exception>
		/// <exception cref="Exceptions.NoLicenseException">This exception thrown only in trial mode if document cannot be opened due to a license restrictions"</exception>
		/// <remarks>
		/// <note type="note">
		/// With the trial version the documents which size is smaller than 1024 Kb, or greater than 10 Mb can be loaded without any restrictions. For other documents the allowed ranges is 1.5 - 2 Mb; 2.5 - 3 Mb; 3.5 - 4 Mb; 4.5 - 5 Mb and so on.
		/// </note> 
		/// </remarks>
		public void LoadDocument(string path, string password = null)
		{
			try {
				CloseDocument();
				Document = PdfDocument.Load(path, _fillForms, password);
				_loadedByViewer = true;
				OnDocumentLoaded(EventArgs.Empty);
			}
			catch (NoLicenseException ex)
			{
				MessageBox.Show(this, ex.Message, Properties.Error.ErrorHeader, MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		}

		/// <summary>
		/// Loads the PDF document from the specified stream.
		/// </summary>
		/// <param name="stream">The stream containing the PDF document to load. The stream must support seeking.</param>
		/// <param name="password">A string used as the password for PDF file. If no password needed, empty or NULL can be used.</param>
		/// <exception cref="Exceptions.UnknownErrorException">unknown error</exception>
		/// <exception cref="Exceptions.PdfFileNotFoundException">file not found or could not be opened</exception>
		/// <exception cref="Exceptions.BadFormatException">file not in PDF format or corrupted</exception>
		/// <exception cref="Exceptions.InvalidPasswordException">password required or incorrect password</exception>
		/// <exception cref="Exceptions.UnsupportedSecuritySchemeException">unsupported security scheme</exception>
		/// <exception cref="Exceptions.PdfiumException">Error occured in PDFium. See ErrorCode for detail</exception>
		/// <exception cref="Exceptions.NoLicenseException">This exception thrown only in trial mode if document cannot be opened due to a license restrictions"</exception>
		/// <remarks>
		/// <note type="note">
		/// <para>The application should maintain the stream resources being valid until the PDF document close.</para>
		/// <para>With the trial version the documents which size is smaller than 1024 Kb, or greater than 10 Mb can be loaded without any restrictions. For other documents the allowed ranges is 1.5 - 2 Mb; 2.5 - 3 Mb; 3.5 - 4 Mb; 4.5 - 5 Mb and so on.</para>
		/// </note> 
		/// </remarks>
		public void LoadDocument(Stream stream, string password = null)
		{
			try {
				CloseDocument();
				Document = PdfDocument.Load(stream, _fillForms, password);
				_loadedByViewer = true;
				OnDocumentLoaded(EventArgs.Empty);
			}
			catch (NoLicenseException ex)
			{
				MessageBox.Show(this, ex.Message, Properties.Error.ErrorHeader, MessageBoxButtons.OK, MessageBoxIcon.Information);
			}

		}

		/// <summary>
		/// Loads the PDF document from the specified byte array.
		/// </summary>
		/// <param name="pdf">The byte array containing the PDF document to load.</param>
		/// <param name="password">A string used as the password for PDF file. If no password needed, empty or NULL can be used.</param>
		/// <exception cref="Exceptions.UnknownErrorException">unknown error</exception>
		/// <exception cref="Exceptions.PdfFileNotFoundException">file not found or could not be opened</exception>
		/// <exception cref="Exceptions.BadFormatException">file not in PDF format or corrupted</exception>
		/// <exception cref="Exceptions.InvalidPasswordException">password required or incorrect password</exception>
		/// <exception cref="Exceptions.UnsupportedSecuritySchemeException">unsupported security scheme</exception>
		/// <exception cref="Exceptions.PdfiumException">Error occured in PDFium. See ErrorCode for detail</exception>
		/// <exception cref="Exceptions.NoLicenseException">This exception thrown only in trial mode if document cannot be opened due to a license restrictions"</exception>
		/// <remarks>
		/// <note type="note">
		/// <para>The application should maintain the byte array being valid until the PDF document close.</para>
		/// <para>With the trial version the documents which size is smaller than 1024 Kb, or greater than 10 Mb can be loaded without any restrictions. For other documents the allowed ranges is 1.5 - 2 Mb; 2.5 - 3 Mb; 3.5 - 4 Mb; 4.5 - 5 Mb and so on.</para>
		/// </note> 
		/// </remarks>
		public void LoadDocument(byte[] pdf, string password = null)
		{
			try {
				CloseDocument();
				Document = PdfDocument.Load(pdf, _fillForms, password);
				_loadedByViewer = true;
				OnDocumentLoaded(EventArgs.Empty);
			}
			catch (NoLicenseException ex)
			{
				MessageBox.Show(this, ex.Message, Properties.Error.InfoHeader, MessageBoxButtons.OK, MessageBoxIcon.Information);
			}

		}

		/// <summary>
		/// Close a loaded PDF document.
		/// </summary>
		public void CloseDocument()
		{
			Document = null;
		}
		#endregion

		#region Constructors and initialization
		/// <summary>
		/// Initializes a new instance of the PdfViewer class.
		/// </summary>
		public PdfViewer()
		{
			BackColor = SystemColors.ControlDark;
			PageBackColor = Color.White;
			PageBorderColor = Color.Black;
			FormHighlightColor = Color.Transparent;
			TextSelectColor = Color.FromArgb(70, Color.SteelBlue.R, Color.SteelBlue.G, Color.SteelBlue.B);
			Zoom = 1;
			PageMargin = new Padding(10);
			Padding = new Padding(10);
			ViewMode = ViewModes.Vertical;
			ShowPageSeparator = true;
			PageSeparatorColor = Color.Gray;
			CurrentPageHighlightColor = Color.FromArgb(170, Color.SteelBlue.R, Color.SteelBlue.G, Color.SteelBlue.B);
			ShowCurrentPageHighlight = true;
			PageAlign = ContentAlignment.MiddleCenter;
			RenderFlags = RenderFlags.FPDF_LCD_TEXT | RenderFlags.FPDF_NO_CATCH;
			TilesCount = 2;
			ShowLoadingIcon = true;
			UseProgressiveRender = true;
			PageAutoDispose = true;

			InitializeComponent();
			DoubleBuffered = true;

			_fillForms = new PdfForms();
			CaptureFillForms(_fillForms);
		}
		#endregion

		#region Overrides for  scrolling
		/// <summary>
		/// Raises the System.Windows.Forms.Control.MouseWheel event.
		/// </summary>
		/// <param name="e">A System.Windows.Forms.MouseEventArgs that contains the event data.</param>
		protected override void OnMouseWheel(MouseEventArgs e)
		{
			CalcAndSetCurrentPage();
			var pos = AutoScrollPosition;
			base.OnMouseWheel(e);
			if(pos!= AutoScrollPosition)
				_prPages.ReleaseCanvas();
		}

		/// <summary>
		/// Raises the System.Windows.Forms.ScrollableControl.Scroll event
		/// </summary>
		/// <param name="se">A System.Windows.Forms.ScrollEventArgs that contains the event data.</param>
		protected override void OnScroll(ScrollEventArgs se)
		{
			CalcAndSetCurrentPage();
			if(se.OldValue!= se.NewValue)
				_prPages.ReleaseCanvas();
			base.OnScroll(se);
		}

		/// <summary>
		///  Gets or sets the location of the auto-scroll position.
		/// </summary>
		public new Point AutoScrollPosition
		{
			get
			{
				return base.AutoScrollPosition;
			}
			set
			{
				var prev = base.AutoScrollPosition;
				base.AutoScrollPosition = value;
				if(prev!= base.AutoScrollPosition)
					_prPages.ReleaseCanvas();
			}
		}
		#endregion

		#region Overrides
		/// <summary>
		/// Raises the Resize event
		/// </summary>
		/// <param name="e">An System.EventArgs that contains the event data.</param>
		protected override void OnResize(EventArgs e)
		{
			if (_skipOnResize)
				return;

			UpdateLayout();
			base.OnResize(e);
		}

		/// <summary>
		/// Raises the System.Windows.Forms.Control.Paint event.
		/// </summary>
		/// <param name="e">A System.Windows.Forms.PaintEventArgs that contains the event data.</param>
		protected override void OnPaint(PaintEventArgs e)
		{
			if (Document != null && _renderRects != null)
			{
				//Normalize info about text selection
				SelectInfo selTmp = NormalizeSelectionInfo();

				//For store coordinates of pages separators
				var separator = new List<Point>();

				//Initialize the Canvas bitmap
				_prPages.InitCanvas(ClientSize);
				bool allPagesAreRendered = true;

				//Drawing PART 1. Page content into canvas and some other things
				for (int i = _startPage; i <= _endPage; i++)
				{
					//Actual coordinates of the page with the scroll
					Rectangle actualRect = CalcActualRect(i);
					if (!actualRect.IntersectsWith(ClientRectangle))
					{
						if(PageAutoDispose)
							Document.Pages[i].Dispose();
						continue; //Page is invisible. Skip it
					}

					//Load page if need
					var pageHandle = Document.Pages[i].Handle;
					if (_prPages.CanvasBitmap == null) 
						_prPages.InitCanvas(ClientSize); //The canvas was dropped due to the execution of scripts on the page while it loading.

					//Draw page background
					DrawPageBackColor(e.Graphics, actualRect.X, actualRect.Y, actualRect.Width, actualRect.Height);
					//Draw page and forms
					bool isPageDrawn = DrawPage(e.Graphics, Document.Pages[i], actualRect);
					allPagesAreRendered &= isPageDrawn;

					if(isPageDrawn)  //Draw fill forms
						DrawFillForms(_prPages.FormsBitmap, Document.Pages[i], actualRect);
					else if (ShowLoadingIcon) //or loading icons
						DrawLoadingIcon(e.Graphics, Document.Pages[i], actualRect);

					//Calc coordinates for page separator
					CalcPageSeparator(actualRect, i, ref separator);
				}

				//Draw Canvas bitmap
				e.Graphics.DrawImageUnscaled(_prPages.CanvasBitmap.Image, 0, 0);
				e.Graphics.DrawImageUnscaled(_prPages.FormsBitmap.Image, 0, 0);

				//Draw pages separators
				DrawPageSeparators(e.Graphics, ref separator);

				//Drawing PART 2.
				for (int i = _startPage; i <= _endPage; i++)
				{
					//Actual coordinates of the page with the scroll
					Rectangle actualRect = CalcActualRect(i);
					if (!actualRect.IntersectsWith(ClientRectangle))
						continue; //Page is invisible. Skip it

					//Draw page border
					DrawPageBorder(e.Graphics, actualRect);
					//Draw fillforms selection
					DrawFillFormsSelection(e.Graphics);
					//Draw text highlight
					if (_highlightedText.ContainsKey(i))
						DrawTextHighlight(e.Graphics, _highlightedText[i], i);
					//Draw text selectionn
					DrawTextSelection(e.Graphics, selTmp, i);
					//Draw current page highlight
					DrawCurrentPageHighlight(e.Graphics, i, actualRect);
				}

				if (!allPagesAreRendered)
					StartInvalidateTimer();
				else if ((RenderFlags & (RenderFlags.FPDF_THUMBNAIL | RenderFlags.FPDF_HQTHUMBNAIL)) != 0)
					_prPages.ReleaseCanvas();
				else if (!UseProgressiveRender)
					_prPages.ReleaseCanvas();

				_selectedRectangles.Clear();
			}

			base.OnPaint(e);
		}

		/// <summary>
		/// Raises the System.Windows.Forms.Control.MouseDoubleClick event.
		/// </summary>
		/// <param name="e">A System.Windows.Forms.MouseEventArgs that contains the event data.</param>
		protected override void OnMouseDoubleClick(MouseEventArgs e)
		{
			if (e.Button == System.Windows.Forms.MouseButtons.Left)
			{
				if (Document != null)
				{
					PointF page_point;
					int idx = DeviceToPage(e.X, e.Y, out page_point);
					if (idx >= 0)
					{
						switch (MouseMode)
						{
							case MouseModes.Default:
							case MouseModes.SelectTextTool:
								ProcessMouseDoubleClickForSelectTextTool(page_point, idx);
								break;
						}
					}
				}
			}

			base.OnMouseDoubleClick(e);
		}

		/// <summary>
		/// Raises the System.Windows.Forms.Control.MouseDown event.
		/// </summary>
		/// <param name="e">A System.Windows.Forms.MouseEventArgs that contains the event data.</param>
		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (e.Button == System.Windows.Forms.MouseButtons.Left)
			{
				if (Document != null)
				{
					PointF page_point;
					int idx = DeviceToPage(e.X, e.Y, out page_point);
					if (idx >= 0)
					{
						Document.Pages[idx].OnLButtonDown(0, page_point.X, page_point.Y);
						SetCurrentPage(idx);

						_mousePressed = true;

						switch (MouseMode)
						{
							case MouseModes.Default:
								ProcessMouseDownDefaultTool(page_point, idx);
								ProcessMouseDownForSelectTextTool(page_point, idx);
								break;
							case MouseModes.SelectTextTool:
								ProcessMouseDownForSelectTextTool(page_point, idx);
								break;
							case MouseModes.PanTool:
								ProcessMouseDownPanTool(e.Location);
								break;

						}
						Invalidate();
					}
				}
			}

			base.OnMouseDown(e);
		}

		/// <summary>
		/// Raises the System.Windows.Forms.Control.MouseMove event.
		/// </summary>
		/// <param name="e">A System.Windows.Forms.MouseEventArgs that contains the event data.</param>
		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (Document != null)
			{
				PointF page_point;
				int idx = DeviceToPage(e.X, e.Y, out page_point);

				if (idx >= 0)
				{
					int ei = Document.Pages[idx].Text.GetCharIndexAtPos(page_point.X, page_point.Y, 10.0f, 10.0f);

					if (!Document.Pages[idx].OnMouseMove(0, page_point.X, page_point.Y))
					{
						if (ei >= 0 && (MouseMode == MouseModes.SelectTextTool || MouseMode== MouseModes.Default))
							Cursor = Cursors.IBeam;
						else
							Cursor = DefaultCursor;

					}

					switch (MouseMode)
					{
						case MouseModes.Default:
							ProcessMouseMoveForDefaultTool(page_point, idx);
							ProcessMouseMoveForSelectTextTool(idx, ei);
							break;
						case MouseModes.SelectTextTool:
							ProcessMouseMoveForSelectTextTool(idx, ei);
							break;
						case MouseModes.PanTool:
							ProcessMouseMoveForPanTool(e.Location);
							break;
					}
				}
			}

			base.OnMouseMove(e);
		}


		/// <summary>
		/// Raises the System.Windows.Forms.Control.MouseUp event.
		/// </summary>
		/// <param name="e">A System.Windows.Forms.MouseEventArgs that contains the event data.</param>
		protected override void OnMouseUp(MouseEventArgs e)
		{
			_mousePressed = false;
			if (Document != null)
			{
				if (_selectInfo.StartPage >= 0)
					OnSelectionChanged(EventArgs.Empty);

				PointF page_point;
				int idx = DeviceToPage(e.X, e.Y, out page_point);
				if (idx >= 0)
				{
					Document.Pages[idx].OnLButtonUp(0, page_point.X, page_point.Y);

					switch (MouseMode)
					{
						case MouseModes.Default:
							ProcessMouseUpForDefaultTool(page_point, idx);
							break;
					}
				}
			}

			base.OnMouseUp(e);
		}

		/// <summary>
		/// Raises the System.Windows.Forms.Control.PreviewKeyDown event.
		/// </summary>
		/// <param name="e">A System.Windows.Forms.PreviewKeyDownEventArgs that contains the event data.</param>
		protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e)
		{
			if (Document != null)
			{
				KeyboardModifiers mod = (KeyboardModifiers)0;

				if ((e.Modifiers & Keys.Control) != 0)
					mod |= KeyboardModifiers.ControlKey;
				if ((e.Modifiers & Keys.Shift) != 0)
					mod |= KeyboardModifiers.ShiftKey;
				if ((e.Modifiers & Keys.Alt) != 0)
					mod |= KeyboardModifiers.AltKey;

				Document.Pages.CurrentPage.OnKeyDown((FWL_VKEYCODE)e.KeyCode, mod);
			}
			base.OnPreviewKeyDown(e);
		}


		/// <summary>
		/// Raises the System.Windows.Forms.Control.KeyUp event.
		/// </summary>
		/// <param name="e">A System.Windows.Forms.KeyEventArgs that contains the event data.</param>
		protected override void OnKeyUp(KeyEventArgs e)
		{
			if (Document != null)
			{
				Document.Pages.CurrentPage.OnKeyUp((FWL_VKEYCODE)e.KeyValue, (KeyboardModifiers)e.Modifiers);
			}
			base.OnKeyUp(e);
		}

		/// <summary>
		/// Determines whether the specified key is a regular input key or a special key that requires preprocessing.
		/// </summary>
		/// <param name="keyData">One of the System.Windows.Forms.Keys values.</param>
		/// <returns>true if the specified key is a regular input key; otherwise, false.</returns>
		protected override bool IsInputKey(Keys keyData)
		{
			if (Document == null)
				return base.IsInputKey(keyData);
			return true;
		}


		#endregion

		#region Protected drawing functions
		/// <summary>
		/// Draws page background
		/// </summary>
		/// <param name="graphics">GDI+ Drawing surface</param>
		/// <param name="x">Actual X position of the page</param>
		/// <param name="y">Actual Y position of the page</param>
		/// <param name="width">Actual width of the page</param>
		/// <param name="height">Actual height of the page</param>
		/// <remarks>
		/// Full page rendering is performed in the following order:
		/// <list type="bullet">
		/// <item><see cref="DrawPageBackColor"/></item>
		/// <item><see cref="DrawPage"/> / <see cref="DrawLoadingIcon"/></item>
		/// <item><see cref="DrawFillForms"/></item>
		/// <item><see cref="DrawPageBorder"/></item>
		/// <item><see cref="DrawFillFormsSelection"/></item>
		/// <item><see cref="DrawTextHighlight"/></item>
		/// <item><see cref="DrawTextSelection"/></item>
		/// <item><see cref="DrawCurrentPageHighlight"/></item>
		/// <item><see cref="DrawPageSeparators"/></item>
		/// </list>
		/// </remarks>
		protected virtual void DrawPageBackColor(Graphics graphics, int x, int y, int width, int height)
		{
			using (var br = new SolidBrush(PageBackColor))
			{
				graphics.FillRectangle(br, x, y, width, height);
			}
		}

		/// <summary>
		/// Draws page content and fillforms
		/// </summary>
		/// <param name="graphics">The drawing surface</param>
		/// <param name="page">Page to be drawn</param>
		/// <param name="actualRect">Page bounds in control coordinates</param>
		/// <remarks>
		/// Full page rendering is performed in the following order:
		/// <list type="bullet">
		/// <item><see cref="DrawPageBackColor"/></item>
		/// <item><see cref="DrawPage"/> / <see cref="DrawLoadingIcon"/></item>
		/// <item><see cref="DrawFillForms"/></item>
		/// <item><see cref="DrawPageBorder"/></item>
		/// <item><see cref="DrawFillFormsSelection"/></item>
		/// <item><see cref="DrawTextHighlight"/></item>
		/// <item><see cref="DrawTextSelection"/></item>
		/// <item><see cref="DrawCurrentPageHighlight"/></item>
		/// <item><see cref="DrawPageSeparators"/></item>
		/// </list>
		/// </remarks>
		/// <returns>true if page was rendered; false if any error is occurred or page is still rendering.</returns>
		protected virtual bool DrawPage(Graphics graphics, PdfPage page, Rectangle actualRect)
		{
			if (actualRect.Width <= 0 || actualRect.Height <= 0)
				return true;

			return _prPages.RenderPage(page, actualRect, PageRotation(page), RenderFlags, UseProgressiveRender);
		}

		/// <summary>
		/// Draw fill forms
		/// </summary>
		/// <param name="bmp"><see cref="PdfBitmap"/> object</param>
		/// <param name="page">Page to be drawn</param>
		/// <param name="actualRect">Page bounds in control coordinates</param>
		/// <remarks>
		/// Full page rendering is performed in the following order:
		/// <list type="bullet">
		/// <item><see cref="DrawPageBackColor"/></item>
		/// <item><see cref="DrawPage"/> / <see cref="DrawLoadingIcon"/></item>
		/// <item><see cref="DrawFillForms"/></item>
		/// <item><see cref="DrawPageBorder"/></item>
		/// <item><see cref="DrawFillFormsSelection"/></item>
		/// <item><see cref="DrawTextHighlight"/></item>
		/// <item><see cref="DrawTextSelection"/></item>
		/// <item><see cref="DrawCurrentPageHighlight"/></item>
		/// <item><see cref="DrawPageSeparators"/></item>
		/// </list>
		/// </remarks>
		protected virtual void DrawFillForms(PdfBitmap bmp, PdfPage page, Rectangle actualRect)
		{
			//Draw fillforms to bitmap
			page.RenderForms(bmp, actualRect.X, actualRect.Y, actualRect.Width, actualRect.Height, PageRotation(page), RenderFlags);
		}

		/// <summary>
		/// Draw loading icon
		/// </summary>
		/// <param name="graphics">GDI+ drawing surface</param>
		/// <param name="page">Page to be drawn</param>
		/// <param name="actualRect">Page bounds in control coordinates</param>
		/// <remarks>
		/// Full page rendering is performed in the following order:
		/// <list type="bullet">
		/// <item><see cref="DrawPageBackColor"/></item>
		/// <item><see cref="DrawPage"/> / <see cref="DrawLoadingIcon"/></item>
		/// <item><see cref="DrawFillForms"/></item>
		/// <item><see cref="DrawPageBorder"/></item>
		/// <item><see cref="DrawFillFormsSelection"/></item>
		/// <item><see cref="DrawTextHighlight"/></item>
		/// <item><see cref="DrawTextSelection"/></item>
		/// <item><see cref="DrawCurrentPageHighlight"/></item>
		/// <item><see cref="DrawPageSeparators"/></item>
		/// </list>
		/// </remarks>
		protected virtual void DrawLoadingIcon(Graphics graphics, PdfPage page, Rectangle actualRect)
		{
			StringFormat sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			graphics.DrawString(LoadingIconText, _loadingFont, Brushes.Black, actualRect, sf);
		}

		/// <summary>
		/// Draws page's border
		/// </summary>
		/// <param name="graphics">The drawing surface</param>
		/// <param name="BBox">Page's bounding box</param>
		/// <remarks>
		/// Full page rendering is performed in the following order:
		/// <list type="bullet">
		/// <item><see cref="DrawPageBackColor"/></item>
		/// <item><see cref="DrawPage"/> / <see cref="DrawLoadingIcon"/></item>
		/// <item><see cref="DrawFillForms"/></item>
		/// <item><see cref="DrawPageBorder"/></item>
		/// <item><see cref="DrawFillFormsSelection"/></item>
		/// <item><see cref="DrawTextHighlight"/></item>
		/// <item><see cref="DrawTextSelection"/></item>
		/// <item><see cref="DrawCurrentPageHighlight"/></item>
		/// <item><see cref="DrawPageSeparators"/></item>
		/// </list>
		/// </remarks>
		protected virtual void DrawPageBorder(Graphics graphics, Rectangle BBox)
		{
			graphics.DrawRectangle(_pageBorderColorPen, BBox);
		}

		/// <summary>
		/// Draws highlights inside a forms
		/// </summary>
		/// <param name="graphics">The drawing surface</param>
		/// <remarks>
		/// Full page rendering is performed in the following order:
		/// <list type="bullet">
		/// <item><see cref="DrawPageBackColor"/></item>
		/// <item><see cref="DrawPage"/> / <see cref="DrawLoadingIcon"/></item>
		/// <item><see cref="DrawFillForms"/></item>
		/// <item><see cref="DrawPageBorder"/></item>
		/// <item><see cref="DrawFillFormsSelection"/></item>
		/// <item><see cref="DrawTextHighlight"/></item>
		/// <item><see cref="DrawTextSelection"/></item>
		/// <item><see cref="DrawCurrentPageHighlight"/></item>
		/// <item><see cref="DrawPageSeparators"/></item>
		/// </list>
		/// </remarks>
		protected virtual void DrawFillFormsSelection(Graphics graphics)
		{
			foreach (var selectRc in _selectedRectangles)
				graphics.FillRectangle(_selectColorBrush, selectRc);
		}

		/// <summary>
		/// Draws text highlights
		/// </summary>
		/// <param name="graphics">The drawing surface</param>
		/// <param name="entries">Highlights info.</param>
		/// <param name="pageIndex">Page index to be drawn</param>
		/// <remarks>
		/// Full page rendering is performed in the following order:
		/// <list type="bullet">
		/// <item><see cref="DrawPageBackColor"/></item>
		/// <item><see cref="DrawPage"/> / <see cref="DrawLoadingIcon"/></item>
		/// <item><see cref="DrawFillForms"/></item>
		/// <item><see cref="DrawPageBorder"/></item>
		/// <item><see cref="DrawFillFormsSelection"/></item>
		/// <item><see cref="DrawTextHighlight"/></item>
		/// <item><see cref="DrawTextSelection"/></item>
		/// <item><see cref="DrawCurrentPageHighlight"/></item>
		/// <item><see cref="DrawPageSeparators"/></item>
		/// </list>
		/// </remarks>
		protected virtual void DrawTextHighlight(Graphics graphics, List<HighlightInfo> entries, int pageIndex)
		{
			foreach (var e in entries)
			{
				var textInfo = Document.Pages[pageIndex].Text.GetTextInfo(e.CharIndex, e.CharsCount);
				foreach (var rc in textInfo.Rects)
				{
					var pt1 = PageToDevice(rc.left, rc.top, pageIndex);
					var pt2 = PageToDevice(rc.right, rc.bottom, pageIndex);
					int x = pt1.X < pt2.X ? pt1.X : pt2.X;
					int y = pt1.Y < pt2.Y ? pt1.Y : pt2.Y;
					int w = pt1.X > pt2.X ? pt1.X - pt2.X : pt2.X - pt1.X;
					int h = pt1.Y > pt2.Y ? pt1.Y - pt2.Y : pt2.Y - pt1.Y;
					graphics.FillRectangle(e.Brush, new Rectangle(x, y, w, h));
				}
			}

		}

		/// <summary>
		/// Draws text selection
		/// </summary>
		/// <param name="graphics">The drawing surface</param>
		/// <param name="selInfo">Selection info</param>
		/// <param name="pageIndex">Page index to be drawn</param>
		/// <remarks>
		/// Full page rendering is performed in the following order:
		/// <list type="bullet">
		/// <item><see cref="DrawPageBackColor"/></item>
		/// <item><see cref="DrawPage"/> / <see cref="DrawLoadingIcon"/></item>
		/// <item><see cref="DrawFillForms"/></item>
		/// <item><see cref="DrawPageBorder"/></item>
		/// <item><see cref="DrawFillFormsSelection"/></item>
		/// <item><see cref="DrawTextHighlight"/></item>
		/// <item><see cref="DrawTextSelection"/></item>
		/// <item><see cref="DrawCurrentPageHighlight"/></item>
		/// <item><see cref="DrawPageSeparators"/></item>
		/// </list>
		/// </remarks>
		protected virtual void DrawTextSelection(Graphics graphics, SelectInfo selInfo, int pageIndex)
		{
			if (selInfo.StartPage < 0 || !_isShowSelection)
				return;
			if (pageIndex >= selInfo.StartPage && pageIndex <= selInfo.EndPage)
			{
				int s = 0;
				if (pageIndex == selInfo.StartPage)
					s = selInfo.StartIndex;

				int len = Document.Pages[pageIndex].Text.CountChars;
				if (pageIndex == selInfo.EndPage)
					len = (selInfo.EndIndex + 1) - s;

				var ti = Document.Pages[pageIndex].Text.GetTextInfo(s, len);
				foreach (var rc in ti.Rects)
				{
					var pt1 = PageToDevice(rc.left, rc.top, pageIndex);
					var pt2 = PageToDevice(rc.right, rc.bottom, pageIndex);

					int x = pt1.X < pt2.X ? pt1.X : pt2.X;
					int y = pt1.Y < pt2.Y ? pt1.Y : pt2.Y;
					int w = pt1.X > pt2.X ? pt1.X - pt2.X : pt2.X - pt1.X;
					int h = pt1.Y > pt2.Y ? pt1.Y - pt2.Y : pt2.Y - pt1.Y;

					graphics.FillRectangle(_selectColorBrush, new Rectangle(x, y, w, h));
				}
			}
		}

		/// <summary>
		/// Draws current page highlight
		/// </summary>
		/// <param name="graphics">The drawing surface</param>
		/// <param name="pageIndex">Page index to be drawn</param>
		/// <param name="actualRect">Page bounds in control coordinates</param>
		/// <remarks>
		/// Full page rendering is performed in the following order:
		/// <list type="bullet">
		/// <item><see cref="DrawPageBackColor"/></item>
		/// <item><see cref="DrawPage"/> / <see cref="DrawLoadingIcon"/></item>
		/// <item><see cref="DrawFillForms"/></item>
		/// <item><see cref="DrawPageBorder"/></item>
		/// <item><see cref="DrawFillFormsSelection"/></item>
		/// <item><see cref="DrawTextHighlight"/></item>
		/// <item><see cref="DrawTextSelection"/></item>
		/// <item><see cref="DrawCurrentPageHighlight"/></item>
		/// <item><see cref="DrawPageSeparators"/></item>
		/// </list>
		/// </remarks>
		protected virtual void DrawCurrentPageHighlight(Graphics graphics, int pageIndex, Rectangle actualRect)
		{
			if (ShowCurrentPageHighlight && pageIndex == Document.Pages.CurrentIndex)
			{
				actualRect.Inflate(0, 0);
				var sm = graphics.SmoothingMode;
				graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
				graphics.DrawRectangle(_currentPageHighlightColorPen, actualRect);
				graphics.SmoothingMode = sm;
			}
		}

		/// <summary>
		/// Draws pages separatoes.
		/// </summary>
		/// <param name="graphics">The drawing surface</param>
		/// <param name="separator">List of pair of points what represents separator</param>
		/// <remarks>
		/// The page rendering is performed in the following order:
		/// <list type="bullet">
		/// <item><see cref="DrawPageBackColor"/></item>
		/// <item><see cref="DrawPage"/> / <see cref="DrawLoadingIcon"/></item>
		/// <item><see cref="DrawFillForms"/></item>
		/// <item><see cref="DrawPageBorder"/></item>
		/// <item><see cref="DrawFillFormsSelection"/></item>
		/// <item><see cref="DrawTextHighlight"/></item>
		/// <item><see cref="DrawTextSelection"/></item>
		/// <item><see cref="DrawCurrentPageHighlight"/></item>
		/// <item><see cref="DrawPageSeparators"/></item>
		/// </list>
		/// </remarks>
		protected virtual void DrawPageSeparators(Graphics graphics, ref List<Point> separator)
		{
			if (separator == null || !ShowPageSeparator)
				return;

			for (int sep = 0; sep < separator.Count; sep += 2)
				graphics.DrawLine(_pageSeparatorColorPen, separator[sep], separator[sep + 1]);
		}
		#endregion

		#region Private methods
		private CaptureInfo CaptureFillForms(PdfForms fillForms)
		{
			var ret = new CaptureInfo();
			if (fillForms == null)
				return ret;

			ret.forms = fillForms;
			ret.sync = fillForms.SynchronizingObject;

			fillForms.SynchronizingObject = this;
			ret.color = fillForms.SetHighlightColor(FormFieldTypes.FPDF_FORMFIELD_UNKNOWN, _formHighlightColor);
			fillForms.AppBeep += FormsAppBeep;
			fillForms.DoGotoAction += FormsDoGotoAction;
			fillForms.DoNamedAction += FormsDoNamedAction;
			fillForms.GotoPage += FormsGotoPage;
			fillForms.Invalidate += FormsInvalidate;
			fillForms.OutputSelectedRect += FormsOutputSelectedRect;
			fillForms.SetCursor += FormsSetCursor;
			return ret;
		}

		private void ReleaseFillForms(CaptureInfo captureInfo)
		{
			if (captureInfo.forms == null)
				return;
			captureInfo.forms.AppBeep -= FormsAppBeep;
			captureInfo.forms.DoGotoAction -= FormsDoGotoAction;
			captureInfo.forms.DoNamedAction -= FormsDoNamedAction;
			captureInfo.forms.GotoPage -= FormsGotoPage;
			captureInfo.forms.Invalidate -= FormsInvalidate;
			captureInfo.forms.OutputSelectedRect -= FormsOutputSelectedRect;
			captureInfo.forms.SetCursor -= FormsSetCursor;
			captureInfo.forms.SynchronizingObject = captureInfo.sync;
			captureInfo.forms.SetHighlightColor(FormFieldTypes.FPDF_FORMFIELD_UNKNOWN, captureInfo.color);
		}

		private void SetScrollPos(int xPos, int yPos)
		{
			AutoScrollPosition = new Point(xPos, yPos);
		}

		private void SetScrollExtent(int width, int height)
        {
            AutoScrollMinSize = new Size(width, height);
        }

		private void CalcAndSetCurrentPage()
		{
			if (Document != null)
			{
				int idx = CalcCurrentPage();
				if (idx >= 0)
				{
					SetCurrentPage(idx);
					Invalidate();
				}
			}
		}

		private void ProcessLinkClicked(PdfLink pdfLink, PdfWebLink webLink)
		{
			var args = new PdfBeforeLinkClickedEventArgs(webLink, pdfLink);
			OnBeforeLinkClicked(args);
			if (args.Cancel)
				return;
			if (pdfLink != null && pdfLink.Destination != null)
				ProcessDestination(pdfLink.Destination);
			else if (pdfLink != null && pdfLink.Action != null)
				ProcessAction(pdfLink.Action);
			else if (webLink != null)
				Process.Start(webLink.Url);
			OnAfterLinkClicked(new PdfAfterLinkClickedEventArgs(webLink, pdfLink));

		}

		private void ProcessDestination(PdfDestination pdfDestination)
		{
			ScrollToPage(pdfDestination.PageIndex);
			Invalidate();
		}

		private void ProcessAction(PdfAction pdfAction)
		{
			if (pdfAction.ActionType == ActionTypes.Uri)
				Process.Start(pdfAction.ActionUrl);
			else if (pdfAction.Destination != null)
				ProcessDestination(pdfAction.Destination);
		}

		private int CalcCurrentPage()
		{
			int idx = -1;
			int maxArea = 0;
			for (int i = _startPage; i <= _endPage; i++)
			{
				var page = Document.Pages[i];

				var rect = RFTR(_renderRects[i]);
				rect.X += AutoScrollPosition.X;
				rect.Y += AutoScrollPosition.Y;
				if (!rect.IntersectsWith(ClientRectangle))
					continue;

				rect.Intersect(ClientRectangle);

				int area = rect.Width * rect.Height;
				if (maxArea < area)
				{
					maxArea = area;
					idx = i;
				}
			}
			return idx;
		}

		private void CalcPageSeparator(Rectangle actualRect, int pageIndex, ref List<Point> separator)
		{
			if (!ShowPageSeparator || pageIndex == _endPage || ViewMode == ViewModes.SinglePage)
				return;
			switch (ViewMode)
			{
				case ViewModes.Vertical:
					separator.Add(new Point(actualRect.X, actualRect.Bottom + PageMargin.Bottom));
					separator.Add(new Point(actualRect.Right, actualRect.Bottom + PageMargin.Bottom));
					break;
				case ViewModes.Horizontal:
					separator.Add(new Point(actualRect.Right + PageMargin.Right, actualRect.Top));
					separator.Add(new Point(actualRect.Right + PageMargin.Right, actualRect.Bottom));
					break;
				case ViewModes.TilesVertical:
					if ((pageIndex+1) % TilesCount != 0)
					{
						//vertical
						separator.Add(new Point(actualRect.Right + PageMargin.Right, actualRect.Top));
						separator.Add(new Point(actualRect.Right + PageMargin.Right, actualRect.Bottom));
					}
					if (pageIndex <= _endPage - TilesCount)
					{
						//horizontal
						separator.Add(new Point(actualRect.X, actualRect.Bottom + PageMargin.Bottom));
						separator.Add(new Point(actualRect.Right, actualRect.Bottom + PageMargin.Bottom));
					}
					break;
			}
		}

		private Rectangle RFTR(RectangleF rect)
		{
			return new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
		}

		private RectangleF GetRenderRect(int index)
		{
			SizeF size = GetRenderSize(index);
			PointF location = GetRenderLocation(size);
			return new RectangleF(location, size);
		}

		private PointF GetRenderLocation(SizeF size)
		{
			var cSize = new Size(
				Size.Width - System.Windows.Forms.SystemInformation.VerticalScrollBarWidth,
				Size.Height - System.Windows.Forms.SystemInformation.HorizontalScrollBarHeight
				);
			float xleft = 0+Padding.Left;
			float ytop = 0+Padding.Top;
			float xcenter = ((float)cSize.Width - Padding.Horizontal - size.Width) / 2 +Padding.Left;
			float ycenter = ((float)cSize.Height - Padding.Vertical - size.Height) / 2 + Padding.Top;
			float xright = (float)cSize.Width - Padding.Horizontal - size.Width + Padding.Left;
			float ybottom = (float)cSize.Height - Padding.Vertical - size.Height + Padding.Top;

			if (xcenter < Padding.Left)
				xcenter = Padding.Left;
			if (ycenter < Padding.Top)
				ycenter = Padding.Top;
			
			switch(PageAlign)
			{
				case ContentAlignment.TopLeft: return new PointF(xleft, ytop);
				case ContentAlignment.TopCenter: return new PointF(xcenter, ytop);
				case ContentAlignment.TopRight: return new PointF(xright, ytop);

				case ContentAlignment.MiddleLeft: return new PointF(xleft, ycenter);
				case ContentAlignment.MiddleCenter: return new PointF(xcenter, ycenter);
				case ContentAlignment.MiddleRight: return new PointF(xright, ycenter);
				
				case ContentAlignment.BottomLeft: return new PointF(xleft, ybottom);
				case ContentAlignment.BottomCenter: return new PointF(xcenter, ybottom);
				case ContentAlignment.BottomRight: return new PointF(xright, ybottom);
				
				default: return new PointF(xcenter, ycenter); 
			}
		}

		private SizeF GetRenderSize(int index)
		{
			var cSize = new Size(
				Size.Width - System.Windows.Forms.SystemInformation.VerticalScrollBarWidth,
				Size.Height - System.Windows.Forms.SystemInformation.HorizontalScrollBarHeight
				);
			double w, h;
			Pdfium.FPDF_GetPageSizeByIndex(Document.Handle, index, out w, out h);

			//converts PDF points which is 1/72 inch to Pixels which is depends on DPI.
			w = w / 72.0 * GetDpi();
			h = h / 72.0 * GetDpi();

			return CalcAppropriateSize(w, h, cSize.Width- Padding.Horizontal, cSize.Height- Padding.Vertical);
		}

		private SizeF CalcAppropriateSize(double w, double h, double fitWidth, double fitHeight)
		{
			if (fitWidth < 0)
				fitWidth = 0;
			if (fitHeight < 0)
				fitHeight = 0;

			double nw = fitWidth;
			double nh = h * nw / w;

			switch (SizeMode)
			{
				case SizeModes.FitToHeight:
					nh = fitHeight;
					nw = w * nh / h;
					break;
				case SizeModes.FitToSize:
					nh = fitHeight;
					nw = w * nh / h;
					if (nw > fitWidth)
					{
						nw = fitWidth;
						nh = h * nw / w;
					}
					break;
				case SizeModes.Zoom:
					nw = w * Zoom;
					nh = h * Zoom;
					break;
			}
			return new SizeF((float)nw, (float)nh);
		}

		private int DeviceToPage(int x, int y, out PointF pagePoint)
		{
			for (int i = _startPage; i <= _endPage; i++)
			{
				var rect = RFTR(_renderRects[i]);
				rect.X += AutoScrollPosition.X;
				rect.Y += AutoScrollPosition.Y;
				if (!rect.Contains(x, y))
					continue;

				pagePoint = Document.Pages[i].DeviceToPage(
					rect.X, rect.Y,
					rect.Width, rect.Height,
					PageRotation(Document.Pages[i]), x, y);

				return i;
			}
			pagePoint = new PointF(0, 0);
			return -1;

		}

		private Point PageToDevice(float x, float y, int pageIndex)
		{
			var rect = RFTR(_renderRects[pageIndex]);
			rect.X += AutoScrollPosition.X;
			rect.Y += AutoScrollPosition.Y;

			return Document.Pages[pageIndex].PageToDevice(
					rect.X, rect.Y,
					rect.Width, rect.Height, 
					PageRotation(Document.Pages[pageIndex]), 
                    x, y);
		}

		private PageRotate PageRotation(PdfPage pdfPage)
		{
			int rot = pdfPage.Rotation - pdfPage.OriginalRotation;
			if (rot < 0)
				rot = 4 + rot;
			return  (PageRotate)rot;
		}

		private SelectInfo NormalizeSelectionInfo()
		{
			var selTmp = _selectInfo;
			if (selTmp.StartPage >= 0 && selTmp.EndPage >= 0)
			{
				if (selTmp.StartPage > selTmp.EndPage)
				{
					selTmp = new SelectInfo()
					{
						StartPage = selTmp.EndPage,
						EndPage = selTmp.StartPage,
						StartIndex = selTmp.EndIndex,
						EndIndex = selTmp.StartIndex
					};
				}
				else if ((selTmp.StartPage == selTmp.EndPage) && (selTmp.StartIndex > selTmp.EndIndex))
				{
					selTmp = new SelectInfo()
					{
						StartPage = selTmp.StartPage,
						EndPage = selTmp.EndPage,
						StartIndex = selTmp.EndIndex,
						EndIndex = selTmp.StartIndex
					};
				}
			}
			return selTmp;
		}

		private SizeF CalcVertical()
		{
			_renderRects = new RectangleF[Document.Pages.Count];
			float y = Padding.Top;
			float width = 0;
			for (int i = 0; i < _renderRects.Length; i++)
			{
				var rrect = GetRenderRect(i);
				_renderRects[i] = new RectangleF(
					rrect.X,
					y + (i > 0 ? PageMargin.Top : 0),
					rrect.Width,
					rrect.Height);
				y += rrect.Height + (_renderRects.Length == 1 ? 0 : (i == 0 || i == _renderRects.Length - 1 ? PageMargin.Bottom : PageMargin.Vertical));
				if (width < rrect.Width)
					width = rrect.Width;
			}
			return new SizeF(width+Padding.Right, y+Padding.Bottom);
		}

		private SizeF CalcTilesVertical()
		{
			_renderRects = new RectangleF[Document.Pages.Count];
			float maxX = 0;
			float maxY = Padding.Top;
			for (int i = 0; i < _renderRects.Length; i += TilesCount)
			{
				float x = 0;
				float y = maxY;
				for (int j = i; j < i + TilesCount; j++)
				{
					if (j >= _renderRects.Length)
						break;
					var rrect = GetRenderRect(j);
					var sz = CalcAppropriateSize(rrect.Width, rrect.Height, rrect.Width - PageMargin.Horizontal * (TilesCount - 1), rrect.Height - PageMargin.Vertical * (TilesCount - 1));
					rrect.Width = sz.Width / TilesCount;
					rrect.Height = sz.Height / TilesCount;

					_renderRects[j] = new RectangleF(
						x + (j != i ? PageMargin.Left : 0) + (j == i ? rrect.X : 0),
						y + (i >= TilesCount ? PageMargin.Top : 0),
						rrect.Width,
						rrect.Height);
					x += rrect.Width + (j == i ? rrect.X : 0) + (j == i ? PageMargin.Right : PageMargin.Horizontal);

					if (maxY < _renderRects[j].Y + _renderRects[j].Height + (j > _renderRects.Length-1 - TilesCount ? 0 : PageMargin.Bottom))
						maxY = _renderRects[j].Y + _renderRects[j].Height + (j > _renderRects.Length-1 - TilesCount ? 0 : PageMargin.Bottom);
					if (maxX < _renderRects[j].X + _renderRects[j].Width + (j == i + TilesCount - 1 ? 0 : PageMargin.Right))
						maxX = _renderRects[j].X + _renderRects[j].Width + (j == i + TilesCount - 1 ? 0 : PageMargin.Right);
				}
			}
			return new SizeF(maxX+Padding.Right, maxY+Padding.Bottom);
		}

		private SizeF CalcTilesVerticalNoChangeSize()
		{
			_renderRects = new RectangleF[Document.Pages.Count];
			float maxX = 0;
			float maxY = Padding.Top;
			for (int i = 0; i < _renderRects.Length; i += TilesCount)
			{
				float x = 0;
				float y = maxY;
				for (int j = i; j < i + TilesCount; j++)
				{
					if (j >= _renderRects.Length)
						break;
					var rrect = GetRenderRect(j);

					_renderRects[j] = new RectangleF(
						x + (j != i ? PageMargin.Left : 0) + (j == i ? rrect.X : 0),
						y + (i >= TilesCount ? PageMargin.Top : 0),
						rrect.Width,
						rrect.Height);
					x += rrect.Width + (j == i ? rrect.X : 0) + (j == i ? PageMargin.Right : PageMargin.Horizontal);

					if (maxY < _renderRects[j].Y + _renderRects[j].Height + (j > _renderRects.Length - 1 - TilesCount ? 0 : PageMargin.Bottom))
						maxY = _renderRects[j].Y + _renderRects[j].Height + (j > _renderRects.Length - 1 - TilesCount ? 0 : PageMargin.Bottom);
					if (maxX < _renderRects[j].X + _renderRects[j].Width + (j == i + TilesCount - 1 ? 0 : PageMargin.Right))
						maxX = _renderRects[j].X + _renderRects[j].Width + (j == i + TilesCount - 1 ? 0 : PageMargin.Right);
				}
			}
			return new SizeF(maxX + Padding.Right, maxY + Padding.Bottom);
		}

		private SizeF CalcHorizontal()
		{
			_renderRects = new RectangleF[Document.Pages.Count];
			float height = 0;
			float x = Padding.Left;
			for (int i = 0; i < _renderRects.Length; i++)
			{
				var rrect = GetRenderRect(i);
				_renderRects[i] = new RectangleF(
					x + (i > 0 ? PageMargin.Left : 0),
					rrect.Y,
					rrect.Width,
					rrect.Height);
				x += rrect.Width + (_renderRects.Length == 1 ? 0 : (i == 0 || i == _renderRects.Length - 1 ? PageMargin.Right : PageMargin.Horizontal));
				if (height < rrect.Height)
					height = rrect.Height;
			}
			return new SizeF(x+Padding.Right, height+Padding.Bottom);
		}

		private SizeF CalcSingle()
		{
			_renderRects = new RectangleF[Document.Pages.Count];
			SizeF ret = new SizeF(0, 0);
			for (int i = 0; i < _renderRects.Length; i++)
			{
				var rrect = GetRenderRect(i);
				_renderRects[i] = new RectangleF(
					rrect.X,
					rrect.Y,
					rrect.Width,
					rrect.Height);
				if (i == Document.Pages.CurrentIndex)
					ret = new SizeF(rrect.Width + Padding.Horizontal, rrect.Height + Padding.Vertical);
			}
			return ret;
		}

		private void SetCurrentPage(int index)
		{
			try
			{
				Document.Pages.CurrentPageChanged -= Pages_CurrentPageChanged;
				if (Document.Pages.CurrentIndex != index)
				{
					var prevIdx = Document.Pages.CurrentIndex;
					Document.Pages.CurrentIndex = index;
					OnCurrentPageChanged(EventArgs.Empty);

					if(ViewMode== ViewModes.SinglePage && prevIdx>0 && prevIdx< Document.Pages.Count && PageAutoDispose)
						Document.Pages[prevIdx].Dispose();
				}
			}
			finally
			{
				Document.Pages.CurrentPageChanged += Pages_CurrentPageChanged;
			}
		}

		private bool CalcIntersectEntries(HighlightInfo existEntry, HighlightInfo addingEntry, out List<HighlightInfo> calcEntries)
		{
			calcEntries = new List<HighlightInfo>();
			int eStart = existEntry.CharIndex;
			int eEnd = existEntry.CharIndex + existEntry.CharsCount - 1;
			int aStart = addingEntry.CharIndex;
			int aEnd = addingEntry.CharIndex + addingEntry.CharsCount - 1;

			if (eStart < aStart && eEnd >= aStart && eEnd <= aEnd)
			{
				calcEntries.Add(new HighlightInfo()
				{
					CharIndex = eStart,
					CharsCount = aStart - eStart,
					Color = existEntry.Color
				});
				return true;
			}
			else if (eStart >= aStart && eStart <= aEnd && eEnd > aEnd)
			{
				calcEntries.Add(new HighlightInfo()
				{
					CharIndex = aEnd + 1,
					CharsCount = eEnd - aEnd,
					Color = existEntry.Color
				});
				return true;
			}
			else if (eStart >= aStart && eEnd <= aEnd)
				return true;
			else if (eStart < aStart && eEnd > aEnd)
			{
				calcEntries.Add(new HighlightInfo()
				{
					CharIndex = eStart,
					CharsCount = aStart - eStart,
					Color = existEntry.Color
				});
				calcEntries.Add(new HighlightInfo()
				{
					CharIndex = aEnd + 1,
					CharsCount = eEnd - aEnd,
					Color = existEntry.Color
				});
				return true;
			}
			//no intersection
			return false;
		}

		private bool GetWord(PdfText text, int ci, out int si, out int ei)
		{
			si = ei = ci;
			if (text == null)
				return false;

			if (ci < 0)
				return false;
			
			for(int i= ci-1; i>=0; i--)
			{
				var c = text.GetCharacter(i);

				if (
					char.IsSeparator(c) || char.IsPunctuation(c) || char.IsControl(c) || 
					char.IsWhiteSpace(c) || c == '\r' || c == '\n'
					)
					break;
				si = i;
			}

			int last = text.CountChars;
            for (int i = ci + 1; i < last; i++ )
			{
				var c = text.GetCharacter(i);

				if (
					char.IsSeparator(c) || char.IsPunctuation(c) || char.IsControl(c) ||
					char.IsWhiteSpace(c) || c == '\r' || c == '\n'
					)
					break;
				ei = i;
			}
			return true;
		}

		private void StartInvalidateTimer()
		{
			if (_invalidateTimer != null)
				return;

			_invalidateTimer = new Timer();
			_invalidateTimer.Interval = 10;
			_invalidateTimer.Tick += (s, a) =>
			{
				if (!_prPages.IsNeedContinuePaint)
				{
					_invalidateTimer.Stop();
					_invalidateTimer = null;
				}
				Invalidate();
			};
			_invalidateTimer.Start();
		}

		[DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
		private static extern int GetDeviceCaps(IntPtr hDC, int nIndex);

		private int GetDpi()
		{
			if (_dpi <= 0)
			{
				//Gets DPI
				using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
				{
					IntPtr desktop = g.GetHdc();
					_dpi = GetDeviceCaps(desktop, 88);
					g.ReleaseHdc(desktop);
				}
			}
			return _dpi;
		}
		#endregion

		#region FillForms event handlers
		/// <summary>
		/// Called by the engine when it is required to redraw the page
		/// </summary>
		/// <param name="e">An <see cref="InvalidatePageEventArgs"/> that contains the event data.</param>
		protected virtual void OnFormsInvalidate(InvalidatePageEventArgs e)
		{
			Invalidate();
		}

		/// <summary>
		/// Called by the engine when it is required to execute GoTo operation
		/// </summary>
		/// <param name="e">An EventArgs that contains the event data.</param>
		protected virtual void OnFormsGotoPage(EventArgs<int> e)
		{
			if (Document == null)
				return;
			SetCurrentPage(e.Value);
			ScrollToPage(e.Value);
		}

		/// <summary>
		/// Called by the engine when it is required to execute a named action
		/// </summary>
		/// <param name="e">An EventArgs that contains the event data.</param>
		protected virtual void OnFormsDoNamedAction(EventArgs<string> e)
		{
			if (Document == null)
				return;
			var dest = Document.NamedDestinations.GetByName(e.Value);
			if (dest != null)
			{
				SetCurrentPage(dest.PageIndex);
				ScrollToPage(dest.PageIndex);
			}
		}

		/// <summary>
		/// Called by the engine when it is required to execute a GoTo action
		/// </summary>
		/// <param name="e">An <see cref="DoGotoActionEventArgs"/> that contains the event data.</param>
		protected virtual void OnFormsDoGotoAction(DoGotoActionEventArgs e)
		{
			if (Document == null)
				_onstartPageIndex = e.PageIndex;
			else
			{
				SetCurrentPage(e.PageIndex);
				ScrollToPage(e.PageIndex);
			}
		}

		/// <summary>
		/// Called by the engine when it is required to play the sound
		/// </summary>
		/// <param name="e">An EventArgs that contains the event data.</param>
		protected virtual void OnFormsAppBeep(EventArgs<BeepTypes> e)
		{
			switch (e.Value)
			{
				case BeepTypes.Default: System.Media.SystemSounds.Beep.Play(); break;
				case BeepTypes.Error: System.Media.SystemSounds.Asterisk.Play(); break;
				case BeepTypes.Question: System.Media.SystemSounds.Question.Play(); break;
				case BeepTypes.Warning: System.Media.SystemSounds.Exclamation.Play(); break;
				case BeepTypes.Status: System.Media.SystemSounds.Beep.Play(); break;
				default: System.Media.SystemSounds.Beep.Play(); break;
			}
		}

		/// <summary>
		/// Called by the engine when it is required to draw selected regions in FillForms
		/// </summary>
		/// <param name="e">An <see cref="InvalidatePageEventArgs"/> that contains the event data.</param>
		protected virtual void OnFormsOutputSelectedRect(InvalidatePageEventArgs e)
		{
			if (Document == null)
				return;
			var idx = Document.Pages.GetPageIndex(e.Page);
			var pt1 = PageToDevice(e.Rect.left, e.Rect.top, idx);
			var pt2 = PageToDevice(e.Rect.right, e.Rect.bottom, idx);
			_selectedRectangles.Add(new Rectangle(pt1.X, pt1.Y, pt2.X - pt1.X, pt2.Y - pt1.Y));
			Invalidate();
		}

		/// <summary>
		/// Called by the engine when it is required to change the cursor
		/// </summary>
		/// <param name="e">An <see cref="SetCursorEventArgs"/> that contains the event data.</param>
		protected virtual void OnFormsSetCursor(SetCursorEventArgs e)
		{
			switch (e.Cursor)
			{
				case CursorTypes.Hand: Cursor = Cursors.Hand; break;
				case CursorTypes.HBeam: Cursor = Cursors.IBeam; break;
				case CursorTypes.VBeam: Cursor = Cursors.IBeam; break;
				case CursorTypes.NESW: Cursor = Cursors.SizeNESW; break;
				case CursorTypes.NWSE: Cursor = Cursors.SizeNWSE; break;
				default: Cursor = DefaultCursor; break;
			}
		}
		#endregion

		#region FillForms event handlers
		private void FormsInvalidate(object sender, InvalidatePageEventArgs e)
		{
			OnFormsInvalidate(e);
		}

		private void FormsGotoPage(object sender, EventArgs<int> e)
		{
			OnFormsGotoPage(e);
		}

		private void FormsDoNamedAction(object sender, EventArgs<string> e)
		{
			OnFormsDoNamedAction(e);
		}

		private void FormsDoGotoAction(object sender, DoGotoActionEventArgs e)
		{
			OnFormsDoGotoAction(e);
		}

		private void FormsAppBeep(object sender, EventArgs<BeepTypes> e)
		{
			OnFormsAppBeep(e);
		}

		private void FormsOutputSelectedRect(object sender, InvalidatePageEventArgs e)
		{
			OnFormsOutputSelectedRect(e);
		}

		private void FormsSetCursor(object sender, SetCursorEventArgs e)
		{
			OnFormsSetCursor(e);
		}
		#endregion

		#region Miscellaneous event handlers
		private void Pages_ProgressiveRender(object sender, ProgressiveRenderEventArgs e)
		{
			e.NeedPause = _prPages.IsNeedPause(sender as PdfPage);
		}

		void Pages_CurrentPageChanged(object sender, EventArgs e)
		{
			if (ViewMode == ViewModes.SinglePage)
				_prPages.ReleaseCanvas();
			OnCurrentPageChanged(EventArgs.Empty);
			Invalidate();
		}

		void Pages_PageInserted(object sender, PageCollectionChangedEventArgs e)
		{
			UpdateLayout();
		}

		void Pages_PageDeleted(object sender, PageCollectionChangedEventArgs e)
		{
			UpdateLayout();

		}

		#endregion

		#region Select tool
		private void ProcessMouseDoubleClickForSelectTextTool(PointF page_point, int page_index)
		{
			var page = Document.Pages[page_index];
			//page.OnLButtonDown(0, page_point.X, page_point.Y);

			int si, ei;
			int ci = page.Text.GetCharIndexAtPos(page_point.X, page_point.Y, 10.0f, 10.0f);
			if (GetWord(page.Text, ci, out si, out ei))
			{
				_selectInfo = new SelectInfo()
				{
					StartPage = page_index,
					EndPage = page_index,
					StartIndex = si,
					EndIndex = ei,
				};
				_isShowSelection = true;
				if (_selectInfo.StartPage >= 0)
					OnSelectionChanged(EventArgs.Empty);
				Invalidate();
			}
		}

		private void ProcessMouseDownForSelectTextTool(PointF page_point, int page_index)
		{
			_selectInfo = new SelectInfo()
			{
				StartPage = page_index,
				EndPage = page_index,
				StartIndex = Document.Pages[page_index].Text.GetCharIndexAtPos(page_point.X, page_point.Y, 10.0f, 10.0f),
				EndIndex = -1// Document.Pages[page_index].Text.GetCharIndexAtPos(page_point.X, page_point.Y, 10.0f, 10.0f),
			};
			_isShowSelection = false;
			if (_selectInfo.StartPage >= 0)
				OnSelectionChanged(EventArgs.Empty);
		}

		private void ProcessMouseMoveForSelectTextTool(int page_index, int character_index)
		{
			if (_mousePressed)
			{
				if (character_index >= 0)
				{
					_selectInfo = new SelectInfo()
					{
						StartPage = _selectInfo.StartPage,
						EndPage = page_index,
						EndIndex = character_index,
						StartIndex = _selectInfo.StartIndex,
					};
					_isShowSelection = true;
				}
				Invalidate();
			}
		}
		#endregion

		#region Default tool
		private void ProcessMouseDownDefaultTool(PointF page_point, int page_index)
		{
			var pdfLink = Document.Pages[page_index].Links.GetLinkAtPoint(page_point);
			var webLink = Document.Pages[page_index].Text.WebLinks.GetWebLinkAtPoint(page_point);
			if (webLink != null || pdfLink != null)
				_mousePressedInLink = true;
			else
				_mousePressedInLink = false;
		}

		private void ProcessMouseMoveForDefaultTool(PointF page_point, int page_index)
		{
			var pdfLink = Document.Pages[page_index].Links.GetLinkAtPoint(page_point);
			var webLink = Document.Pages[page_index].Text.WebLinks.GetWebLinkAtPoint(page_point);
			if (webLink != null || pdfLink != null)
				Cursor = Cursors.Hand;
		}

		private void ProcessMouseUpForDefaultTool(PointF page_point, int page_index)
		{
			if (_mousePressedInLink)
			{
				var pdfLink = Document.Pages[page_index].Links.GetLinkAtPoint(page_point);
				var webLink = Document.Pages[page_index].Text.WebLinks.GetWebLinkAtPoint(page_point);
				if (webLink != null || pdfLink != null)
					ProcessLinkClicked(pdfLink, webLink);
			}
		}
		#endregion

		#region Pan tool
		private void ProcessMouseDownPanTool(Point mouse_point)
		{
			_panToolInitialScrollPosition = AutoScrollPosition;
			_panToolInitialMousePosition = mouse_point;
		}

		private void ProcessMouseMoveForPanTool(Point mouse_point)
		{
			if (!_mousePressed)
				return;
			var yOffs = mouse_point.Y - _panToolInitialMousePosition.Y;
			var xOffs = mouse_point.X - _panToolInitialMousePosition.X;
            SetScrollPos(-_panToolInitialScrollPosition.X - xOffs, -_panToolInitialScrollPosition.Y - yOffs);
		}
		#endregion
	}
}
