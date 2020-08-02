﻿using CoreComicsConverter.Extensions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CoreComicsConverter
{
    public class PdfParser : IEventListener
    {
        private ConcurrentQueue<int> _pageQueue;

        private ConcurrentBag<(int width, int height)> _imageSizes;

        private List<Exception> _parserErrors;

        public void SetPageCount(Pdf pdf)
        {
            using var pdfReader = new PdfReader(pdf.PdfPath);

            var pdfDoc = new PdfDocument(pdfReader);

            if (pdfReader.IsEncrypted())
            {
                throw new ApplicationException(pdf.PdfPath + " is encrypted.");
            }


            pdf.PageCount = pdfDoc.GetNumberOfPages();
        }

        public List<(int width, int height, int count)> ParseImages(Pdf pdf)
        {
            if (pdf.PageCount == 0)
            {
                SetPageCount(pdf);
            }

            var pages = Enumerable.Range(1, pdf.PageCount);
            _pageQueue = new ConcurrentQueue<int>(pages);

            _imageSizes = new ConcurrentBag<(int, int)>();
            _parserErrors = new List<Exception>();

            Parallel.For(0, Program.ParallelThreads, (index, state) => ProcessPages(pdf, state));

            pdf.ImageCount = _imageSizes.Count;

            var imageSizesMap = BuildImageSizesMap();

            var list = imageSizesMap.Values.OrderByDescending(x => x.count).AsList();
            return list;
        }

        public List<Exception> GetImageParserErrors()
        {
            return _parserErrors ?? new List<Exception>();
        }

        private Dictionary<string, (int width, int height, int count)> BuildImageSizesMap()
        {
            var imageSizesMap = new Dictionary<string, (int, int, int count)>();

            foreach (var (width, height) in _imageSizes)
            {
                var key = $"{width} x {height}";

                var count = imageSizesMap.TryGetValue(key, out var existingImageSize) ? existingImageSize.count + 1 : 1;

                imageSizesMap[key] = (width, height, count);
            }

            return imageSizesMap;
        }

        private void ProcessPages(Pdf pdf, ParallelLoopState loopState)
        {
            if (_pageQueue.IsEmpty)
            {
                loopState.Stop();
                return;
            }

            using var pdfReader = new PdfReader(pdf.PdfPath);
            using var pdfDoc = new PdfDocument(pdfReader);

            var pdfDocParser = new PdfDocumentContentParser(pdfDoc);

            while (!_pageQueue.IsEmpty)
            {
                if (_pageQueue.TryDequeue(out var currentPage))
                {
                    pdfDocParser.ProcessContent(currentPage, this);

                    PageParsed?.Invoke(this, new PageParsedEventArgs(currentPage));
                }
            }
        }

        public ICollection<EventType> GetSupportedEvents()
        {
            return new[] { EventType.RENDER_IMAGE };
        }

        public void EventOccurred(IEventData data, EventType type)
        {
            try
            {
                var renderInfo = data as ImageRenderInfo;

                var imageObject = renderInfo.GetImage();

                var width = imageObject.GetWidth();
                var height = imageObject.GetHeight();

                _imageSizes.Add((Convert.ToInt32(width), Convert.ToInt32(height)));
            }
            catch (Exception ex)
            {
                _parserErrors.Add(ex);
            }
        }

        public event EventHandler<PageParsedEventArgs> PageParsed;
    }
}