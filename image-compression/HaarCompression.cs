﻿using System;
using System.Drawing;
using System.Collections.Generic;

public class HaarCompression
{
    private int boxSize = 8;
    private int nonZeroOriginal = 0;
    private int nonZeroCompressed = 0;

    public Tuple<Image, double> compressImage(Image image, int ratio)
    {
        Bitmap bitmap = new Bitmap(image);
        Bitmap compressedImage = new Bitmap(bitmap.Width, bitmap.Height);

        for (int i = 0; i < image.Width; i += this.boxSize)
        {
            for (int j = 0; j < image.Height; j += this.boxSize)
            {
                RGB box = getNextBox(bitmap, i, j);
                RGB compressedBox = compressColors(box, ratio);
                restoreImageFromRGB(ref compressedImage, compressedBox, i, j);
            }
        }

        Tuple<Image, double> result = new Tuple<Image, double>(compressedImage, (double)nonZeroOriginal / nonZeroCompressed);

        return result;
    }

    private void restoreImageFromRGB(ref Bitmap image, RGB compressedBox, int l, int r)
    {
        restoreMatrix(compressedBox.getRedPixelsMatrix());
        restoreMatrix(compressedBox.getGreenPixelsMatrix());
        restoreMatrix(compressedBox.getBluePixelsMatrix());

        for (int i = l; i < image.Width && i < l + this.boxSize; ++i)
        {
            for (int j = r; j < image.Height && j < r + this.boxSize; ++j)
            {
                Color pix = Color.FromArgb((int)compressedBox.getRedPixel(i - l, j - r),
                    (int)compressedBox.getGreenPixel(i - l, j - r),
                    (int)compressedBox.getBluePixel(i - l, j - r));

                image.SetPixel(i, j, pix);
            }
        }
    }

    private RGB getNextBox(Bitmap image, int l, int r)
    {
        RGB box = new RGB(this.boxSize);
        for (int i = l; i < image.Width && i < l + this.boxSize; ++i)
        {
            for (int j = r; j < image.Height && j < r + this.boxSize; ++j)
            {
                Color pix = image.GetPixel(i, j);
                box.setRedPixel(pix.R, i - l, j - r);
                box.setGreenPixel(pix.G, i - l, j - r);
                box.setBluePixel(pix.B, i - l, j - r);
            }
        }

        return box;
    }

    private RGB compressColors(RGB originalRGB, int compressionRate)
    {
        RGB compressedRGB = new RGB(originalRGB);

        compressMatrixByHaar(compressedRGB.getRedPixelsMatrix(), compressionRate);
        compressMatrixByHaar(compressedRGB.getGreenPixelsMatrix(), compressionRate);
        compressMatrixByHaar(compressedRGB.getBluePixelsMatrix(), compressionRate);

        return compressedRGB;   
    }

    private double[][] compressMatrixByHaar(double[][] matrix, int compressionRate)
    {

        for (int i = 0; i < this.boxSize; ++i)
        {
            int columns = this.boxSize;
            double[] temp = new double[this.boxSize];

            while (columns > 0)
            {
                for (int j = 0; j < columns / 2; ++j)
                {
                    temp[j] = (matrix[i][2 * j] + matrix[i][2 * j + 1]) / 2;
                }

                int k = columns / 2;
                for (int j = 0; j < columns / 2; ++j, ++k)
                {
                    temp[k] = (matrix[i][2 * j] - matrix[i][2 * j + 1]) / 2;
                }

                for (int j = 0; j < this.boxSize; ++j)
                {
                    matrix[i][j] = temp[j];
                }

                columns /= 2;
            }
        }

        for (int i = 0; i < this.boxSize; ++i)
        {
            int rows = this.boxSize;
            double[] temp = new double[this.boxSize];
            while (rows > 0)
            {
                for (int j = 0; j < rows / 2; ++j)
                {
                    temp[j] = (matrix[2 * j][i] + matrix[2 * j + 1][i]) / 2;
                }

                int k = rows / 2;
                for (int j = 0; j < rows / 2; ++j, ++k)
                {
                    temp[k] = (matrix[2 * j][i] - matrix[2 * j + 1][i]) / 2;
                }

                for (int j = 0; j < this.boxSize; ++j)
                {
                    matrix[j][i] = temp[j];
                }

                rows /= 2;
            }
        }

        List<MatrixElement> elements = new List<MatrixElement>();

        int nonZeroPixelsCounterOriginal = 0;

        for (int i = 0; i < this.boxSize; ++i)
        {
            for (int j = 0; j < this.boxSize; ++j)
            {
                if (matrix[i][j] > 0)
                {
                    ++nonZeroPixelsCounterOriginal;
                    elements.Add(new MatrixElement(i, j, matrix[i][j]));
                }
            }
        }

        int elementsToThrowAway = (int) ((elements.Count / 100.0) * (100 - compressionRate));

        elements.Sort();

        int thrownCount = 0;
        while (thrownCount < elementsToThrowAway && thrownCount < elements.Count)
        {
            Tuple<Int32, Int32> pos = elements[thrownCount].getPosition();
            matrix[pos.Item1][pos.Item2] = 0;
            ++thrownCount;
        }

        nonZeroOriginal += nonZeroPixelsCounterOriginal;
        nonZeroCompressed += nonZeroPixelsCounterOriginal - thrownCount;

        return matrix;
    }

    struct MatrixElement : IComparable<MatrixElement>
    {
        Tuple<Int32, Int32> position;
        double value;

        public MatrixElement(int i, int j, double value)
        {
            this.position = new Tuple<int, int>(i, j);
            this.value = value;
        }

        public int CompareTo(MatrixElement other)
        {
            if (this.value == other.value)
            {
                return 0;
            }

            return this.value < other.value ? -1 : 1;
        }

        public Tuple<Int32, Int32> getPosition()
        {
            return this.position;
        }
    }

    private double[][] restoreMatrix(double[][] matrix)
    {
        for (int i = 0; i < this.boxSize; ++i)
        {
            int columns = 1;
            double[] temp = new double[this.boxSize];
            while (columns * 2 <= this.boxSize)
            {
                for (int j = 0; j < this.boxSize; ++j)
                {
                    temp[j] = matrix[i][j];
                }

                for (int j = 0; j < columns; ++j)
                {
                    matrix[i][2 * j] = temp[j] + temp[j + columns];
                    matrix[i][2 * j + 1] = temp[j] - temp[j + columns];

                    if (matrix[i][2 * j] > 255)
                    {
                        matrix[i][2 * j] = 255;
                    }
                    if (matrix[i][2 * j + 1] > 255)
                    {
                        matrix[i][2 * j + 1] = 255;
                    }
                }

                columns *= 2;
            }
        }

        for (int i = 0; i < this.boxSize; ++i)
        {
            int rows = 1;
            double[] temp = new double[this.boxSize];
            while (rows * 2 <= this.boxSize)
            {
                for (int j = 0; j < this.boxSize; ++j)
                {
                    temp[j] = matrix[j][i];
                }

                for (int j = 0; j < rows; ++j)
                {
                    matrix[2 * j][i] = temp[j] + temp[j + rows];
                    matrix[2 * j + 1][i] = temp[j] - temp[j + rows];

                    if (matrix[2 * j][i] > 255)
                    {
                        matrix[2 * j][i] = 255;
                    }
                    if (matrix[2 * j + 1][i] > 255)
                    {
                        matrix[2 * j + 1][i] = 255;
                    }
                }


                rows *= 2;
            }
        }

        return matrix;
    }

    private class RGB
    {
        private double[][] red;
        private double[][] green;
        private double[][] blue;

        private int boxSize = 1;

        public RGB(int boxSize)
        {
            this.boxSize = boxSize;

            this.red = new double[this.boxSize][];
            this.green = new double[this.boxSize][];
            this.blue = new double[this.boxSize][];

            for (int i = 0; i < this.boxSize; ++i)
            {
                this.red[i] = new double[this.boxSize];
                this.green[i] = new double[this.boxSize];
                this.blue[i] = new double[this.boxSize];
            }
        }

        public RGB(RGB copy) : this(copy.boxSize)
        {
            for (int i = 0; i < this.boxSize; ++i)
            {
                for (int j = 0; j < this.boxSize; ++j)
                {
                    this.red[i][j] = copy.getRedPixel(i, j);
                    this.green[i][j] = copy.getGreenPixel(i, j);
                    this.blue[i][j] = copy.getBluePixel(i, j);
                }
            }
        }

        public void setRedPixel(double value, int i, int j)
        {
            this.red[i][j] = value;
        }

        public void setGreenPixel(double value, int i, int j)
        {
            this.green[i][j] = value;
        }

        public void setBluePixel(double value, int i, int j)
        {
            this.blue[i][j] = value;
        }

        public double getRedPixel(int i, int j)
        {
            if (this.red[i][j] < 0)
            {
                return 0;
            }
            return this.red[i][j];
        }

        public double getGreenPixel(int i, int j)
        {
            if (this.green[i][j] < 0)
            {
                return 0;
            }

            return this.green[i][j];
        }

        public double getBluePixel(int i, int j)
        {

            if (this.blue[i][j] < 0)
            {
                return 0;
            }
            return this.blue[i][j];
        }

        public double[][] getRedPixelsMatrix() {
            return this.red;
        }

        public double[][] getGreenPixelsMatrix()
        {
            return this.green;
        }

        public double[][] getBluePixelsMatrix()
        {
            return this.blue;
        }

        public int getBoxSize()
        {
            return this.boxSize;
        }
    }
}
