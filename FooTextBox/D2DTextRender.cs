/*
 * Copyright (C) 2013 FooProject
 * * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program. If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Drawing;
using FooEditEngine;
using SharpDX;
using D2D = SharpDX.Direct2D1;
using DW = SharpDX.DirectWrite;
using DXGI = SharpDX.DXGI;
using System.Runtime.InteropServices;

namespace FooEditEngine.Windows
{

    sealed class D2DTextRender : D2DRenderCommon, IEditorRender, IDisposable
    {
        FooTextBox TextBox;
        string fontName;
        float fontSize;

        public D2DTextRender(FooTextBox textbox)
        {
            this.TextBox = textbox;

            textbox.SizeChanged += new EventHandler(textbox_SizeChanged);
            textbox.FontChanged += new EventHandler(textbox_FontChanged);

            Size size = textbox.Size;
            this.fontName = textbox.Font.Name;
            this.fontSize = textbox.Font.Size;
            this.InitTextFormat(textbox.Font.Name, (float)textbox.Font.Size);
            //初期化ができないので適当なサイズで作る
            this.ReConstructRenderAndResource(100, 100);
        }

        public override void GetDpi(out float dpix, out float dpiy)
        {
            IntPtr hDc = NativeMethods.GetDC(IntPtr.Zero);
            dpix = NativeMethods.GetDeviceCaps(hDc, NativeMethods.LOGPIXELSX);
            dpiy = NativeMethods.GetDeviceCaps(hDc, NativeMethods.LOGPIXELSY);
            NativeMethods.ReleaseDC(IntPtr.Zero, hDc);
        }

        void textbox_FontChanged(object sender, EventArgs e)
        {
            FooTextBox textbox = (FooTextBox)sender;
            Font font = textbox.Font;
            this.fontName = font.Name;
            this.fontSize = font.Size;
            DW.FontWeight weigth = font.Bold ? DW.FontWeight.Bold : DW.FontWeight.Normal;
            DW.FontStyle style = font.Italic ? DW.FontStyle.Italic : DW.FontStyle.Normal;
            this.InitTextFormat(font.Name, font.Size,weigth,style);
        }

        void textbox_SizeChanged(object sender, EventArgs e)
        {
            FooTextBox textbox = (FooTextBox)sender;
            this.ReConstructRenderAndResource(this.TextBox.Width, this.TextBox.Height);
        }

        public static Color4 ToColor4(System.Drawing.Color color)
        {
            return new Color4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
        }


        public void DrawOneLine(Document doc,LineToIndexTable lti, int row, double x, double y)
        {
            this.DrawOneLine(doc,
                lti,
                row,
                x,
                y,
                null);
        }

        public override void DrawCachedBitmap(Rectangle rect)
        {
            if (this.render == null || this.render.IsDisposed)
                return;
            render.DrawBitmap(this.cachedBitMap, rect, 1.0f, D2D.BitmapInterpolationMode.Linear, rect);
        }

        public override void CacheContent()
        {
            if (this.render == null || this.cachedBitMap == null || this.cachedBitMap.IsDisposed || this.render.IsDisposed)
                return;
            //render.Flush();
            this.cachedBitMap.CopyFromRenderTarget(this.render, new SharpDX.Point(), new SharpDX.Rectangle(0, 0, (int)this.renderSize.Width, (int)this.renderSize.Height));
            this.hasCache = true;
        }

        public void DrawContent(EditView view,Rectangle updateRect)
        {
            base.BegineDraw();
            view.Draw(updateRect);
            base.EndDraw();
        }

        public void ReConstructRenderAndResource(double width, double height)
        {
            this.DestructRenderAndResource();
            this.ConstructRenderAndResource(width, height);
        }

        public void ConstructRenderAndResource(double width, double height)
        {
            float dpiX, dpiY;
            this.GetDpi(out dpiX, out dpiY);
            D2D.RenderTargetProperties prop = new D2D.RenderTargetProperties(
                D2D.RenderTargetType.Default,
                new D2D.PixelFormat(DXGI.Format.B8G8R8A8_UNorm, D2D.AlphaMode.Premultiplied),
                dpiX,
                dpiY,
                D2D.RenderTargetUsage.None,
                D2D.FeatureLevel.Level_DEFAULT);

            D2D.HwndRenderTargetProperties hwndProp = new D2D.HwndRenderTargetProperties();
            hwndProp.Hwnd = this.TextBox.Handle;
            hwndProp.PixelSize = new SharpDX.Size2((int)(this.TextBox.Size.Width * this.GetScale()), (int)(this.TextBox.Size.Height * this.GetScale()));
            hwndProp.PresentOptions = D2D.PresentOptions.Immediately;
            this.render = new D2D.WindowRenderTarget(D2DRenderShared.D2DFactory, prop, hwndProp);

            D2D.BitmapProperties bmpProp = new D2D.BitmapProperties();
            bmpProp.DpiX = dpiX;
            bmpProp.DpiY = dpiY;
            bmpProp.PixelFormat = new D2D.PixelFormat(DXGI.Format.B8G8R8A8_UNorm, D2D.AlphaMode.Premultiplied);
            this.cachedBitMap = new D2D.Bitmap(this.render, new SharpDX.Size2((int)width, (int)height), bmpProp);
            this.hasCache = false;

            this.textRender = new CustomTextRenderer(this.Brushes, this.Strokes, this.Foreground);

            this.renderSize = new Size(width, height);

            //デフォルト値を反映させる
            this.Foreground = ToColor4(this.TextBox.Foreground);
            this.Background = ToColor4(this.TextBox.Background);
            this.ControlChar = ToColor4(this.TextBox.ControlChar);
            this.Url = ToColor4(this.TextBox.Url);
            this.Keyword1 = ToColor4(this.TextBox.Keyword1);
            this.Keyword2 = ToColor4(this.TextBox.Keyword2);
            this.Literal = ToColor4(this.TextBox.Literal);
            this.Comment = ToColor4(this.TextBox.Comment);
            this.Hilight = ToColor4(this.TextBox.Hilight);
            this.LineMarker = ToColor4(this.TextBox.LineMarker);
            this.InsertCaret = ToColor4(this.TextBox.InsertCaret);
            this.OverwriteCaret = ToColor4(this.TextBox.OverwriteCaret);
            this.UpdateArea = ToColor4(this.TextBox.UpdateArea);
            this.HilightForeground = ToColor4(this.TextBox.HilightForeground);
        }

        public void DestructRenderAndResource()
        {
            this.hasCache = false;
            if (this.cachedBitMap != null)
                this.cachedBitMap.Dispose();
            this.Brushes.Clear();
            this.Strokes.Clear();
            if (this.textRender != null)
                this.textRender.Dispose();
            if (this.render != null)
                this.render.Dispose();
        }
    }

    internal static class NativeMethods
    {
        public const int LOGPIXELSX = 88;
        public const int LOGPIXELSY = 90;

        [DllImport("gdi32.dll")]
        public static extern int GetDeviceCaps(IntPtr hDc, int nIndex);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);
    }
}
