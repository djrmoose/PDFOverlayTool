using System.Windows;

namespace PdfOverlayTool
{
    /// <summary>
    /// Required third-party license texts for components shipped with the app.
    /// </summary>
    public static class ThirdPartyNotices
    {
        public const string PdfToImageVersion = "5.2.1";
        public const string SkiaSharpVersion = "3.119.2";
        public const string PdfiumVersion = "147.0.7690";

        public static string Body =>
            "This application includes third-party open-source components. " +
            "The notices below are provided as required by their licenses.\n\n" +
            PdfToImageNotice + "\n\n" +
            SkiaSharpNotice + "\n\n" +
            SkiaNotice + "\n\n" +
            PdfiumNotice + "\n\n" +
            BblanchonPdfiumNotice;

        private const string PdfToImageNotice =
            "PDFtoImage " + PdfToImageVersion + "\n" +
            "Copyright (c) 2021-2025 David Sungaila\n" +
            "https://github.com/sungaila/PDFtoImage\n\n" +
            "MIT License\n\n" +
            "Permission is hereby granted, free of charge, to any person obtaining a copy " +
            "of this software and associated documentation files (the \"Software\"), to deal " +
            "in the Software without restriction, including without limitation the rights " +
            "to use, copy, modify, merge, publish, distribute, sublicense, and/or sell " +
            "copies of the Software, and to permit persons to whom the Software is " +
            "furnished to do so, subject to the following conditions:\n\n" +
            "The above copyright notice and this permission notice shall be included in all " +
            "copies or substantial portions of the Software.\n\n" +
            "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR " +
            "IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, " +
            "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE " +
            "AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER " +
            "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, " +
            "OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE " +
            "SOFTWARE.";

        private const string SkiaSharpNotice =
            "SkiaSharp (>= " + SkiaSharpVersion + ") and SkiaSharp.NativeAssets.Win32\n" +
            "Copyright (c) .NET Foundation and Contributors\n" +
            "https://github.com/mono/SkiaSharp\n\n" +
            "MIT License\n\n" +
            "Permission is hereby granted, free of charge, to any person obtaining a copy " +
            "of this software and associated documentation files (the \"Software\"), to deal " +
            "in the Software without restriction, including without limitation the rights " +
            "to use, copy, modify, merge, publish, distribute, sublicense, and/or sell " +
            "copies of the Software, and to permit persons to whom the Software is " +
            "furnished to do so, subject to the following conditions:\n\n" +
            "The above copyright notice and this permission notice shall be included in all " +
            "copies or substantial portions of the Software.\n\n" +
            "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR " +
            "IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, " +
            "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE " +
            "AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER " +
            "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, " +
            "OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE " +
            "SOFTWARE.";

        private const string SkiaNotice =
            "Skia Graphics Library (used by SkiaSharp)\n" +
            "Copyright (c) 2011 Google Inc. All rights reserved.\n" +
            "https://skia.org/\n\n" +
            "Redistribution and use in source and binary forms, with or without " +
            "modification, are permitted provided that the following conditions are met:\n\n" +
            "* Redistributions of source code must retain the above copyright notice, this " +
            "list of conditions and the following disclaimer.\n\n" +
            "* Redistributions in binary form must reproduce the above copyright notice, this " +
            "list of conditions and the following disclaimer in the documentation and/or " +
            "other materials provided with the distribution.\n\n" +
            "* Neither the name of Google Inc. nor the names of its contributors may be used " +
            "to endorse or promote products derived from this software without specific prior " +
            "written permission.\n\n" +
            "THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS \"AS IS\" " +
            "AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE " +
            "IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE " +
            "DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR " +
            "ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES " +
            "(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; " +
            "LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON " +
            "ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT " +
            "(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS " +
            "SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.";

        private const string PdfiumNotice =
            "PDFium (>= " + PdfiumVersion + ")\n" +
            "Copyright 2014 The PDFium Authors. All rights reserved.\n" +
            "https://pdfium.googlesource.com/pdfium/\n\n" +
            "Redistribution and use in source and binary forms, with or without " +
            "modification, are permitted provided that the following conditions are met:\n\n" +
            "* Redistributions of source code must retain the above copyright notice, this " +
            "list of conditions and the following disclaimer.\n\n" +
            "* Redistributions in binary form must reproduce the above copyright notice, " +
            "this list of conditions and the following disclaimer in the documentation " +
            "and/or other materials provided with the distribution.\n\n" +
            "* Neither the name of Google Inc. nor the names of its contributors may be " +
            "used to endorse or promote products derived from this software without specific " +
            "prior written permission.\n\n" +
            "THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS \"AS IS\" " +
            "AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE " +
            "IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE " +
            "DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR " +
            "ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES " +
            "(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; " +
            "LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON " +
            "ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT " +
            "(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS " +
            "SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.";

        private const string BblanchonPdfiumNotice =
            "bblanchon.PDFium.Win32 (>= " + PdfiumVersion + ")\n" +
            "Copyright 2014-2025 Benoit Blanchon\n" +
            "https://github.com/bblanchon/pdfium-binaries\n\n" +
            "MIT License\n\n" +
            "Permission is hereby granted, free of charge, to any person obtaining a copy " +
            "of this software and associated documentation files (the \"Software\"), to deal " +
            "in the Software without restriction, including without limitation the rights " +
            "to use, copy, modify, merge, publish, distribute, sublicense, and/or sell " +
            "copies of the Software, and to permit persons to whom the Software is " +
            "furnished to do so, subject to the following conditions:\n\n" +
            "The above copyright notice and this permission notice shall be included in all " +
            "copies or substantial portions of the Software.\n\n" +
            "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR " +
            "IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, " +
            "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE " +
            "AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER " +
            "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, " +
            "OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE " +
            "SOFTWARE.";

        public static void ShowDialog(Window? owner = null)
        {
            var window = new ThirdPartyNoticesWindow
            {
                Owner = owner
            };
            window.ShowDialog();
        }
    }
}
