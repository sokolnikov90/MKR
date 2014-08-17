using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;

namespace MKR_sokol
{
    public partial class Form1 : Form
    {
        static double dx = 0.2, dy = 0.2; //Значения по умолчанию
        static double dt = 1.0; //Значение по умолчанию
        static int steps = 25; //Число шагов для расчета по умолчанию
        static double lambda = 1; //Коэффициент теплопроводности
        static double C = 1; //Теплоемкость
        static double density = 1; //Плотность
        static double eps = Math.Sqrt(dx * dx + dy * dy) / 1E+12;
        static double k = lambda / (density * C);
        static int NodesNum; //Число узлов
        static int xp = 0, yp = 0; //Пиксельные координаты
        Bitmap btmFront = new Bitmap(1240, 640); //Пиксельное поле для рисования
        Bitmap btmColorMap = new Bitmap(720, 10); //Пиксельное поле для легенды
        Color c = Color.Blue; //Цветовая переменная
        static int dxp, dyp, dxph, dyph; //Пиксельные шаги и полушаги
        static double hue, sat, val, dr, dg, db; //Параметры цветовых моделей HSV и RGB
        static string temperature, numero;


        public Form1()
        {
            InitializeComponent();

            pictureBox2.Image = btmColorMap;

            //Рисование легенды 
            for (int i = 0; i < 720; i++)
            {
                if (i % 72 == 0 && i != 0)
                    c = Color.Black;
                else
                {
                    hue = 0.66 - (double)i / 1091;
                    sat = 1.0;
                    val = 1.0;
                    HSVtoRGB(hue, sat, val, out dr, out dg, out db);
                    c = Color.FromArgb((int)(dr * 255), (int)(dg * 255), (int)(db * 255));
                }

                for (int j = 0; j < 10; j++)
                    btmColorMap.SetPixel(i, j, c);
            }
        }



        private void button1_Click(object sender, EventArgs e)
        {
            //Блокировка полей ввода
            textBox1.ReadOnly = true;
            textBox2.ReadOnly = true;
            textBox3.ReadOnly = true;

            //Инициализация переменных значениями из полей ввода с отловом ошибочных данных
            try
            {
                dx = Convert.ToDouble(textBox1.Text);
                dy = dx;
                dt = Convert.ToDouble(textBox2.Text);
                steps = Convert.ToInt32(textBox3.Text);
            }
            catch (FormatException)
            {
                MessageBox.Show("Использованы значения по умолчанию: dx=0,2; dt = 1; i = 25", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

            //Подсчет количества узлов
            NodesNum = 0;
            double x, y; //Используются для подсчета количества узлов и последующей их инициализации

            for (y = 0; y - 4.0 < eps; y += dy)
                for (x = 0; x - 8.0 < eps; x += dx)
                    if (Validate(x, y))
                        NodesNum++;

            //Создание узлов, определение их координат
            Node[] pt = new Node[1];
            Node[] node = new Node[NodesNum];

            for (int i = 0; i < NodesNum; i++)
                node[i] = new Node();

            int NodeNum = 0;
            int eo = 0;
            double EdgeX = 0.0;

            for (y = 0; y - 4.0 < eps; y += dy)
            {
                if (eo % 2 == 0)
                {
                    for (x = 0; x - 8.0 < eps; x += dx)
                        if (Validate(x, y))
                        {
                            node[NodeNum].x = x;
                            node[NodeNum].y = y;
                            node[NodeNum].number = NodeNum;
                            NodeNum++;

                            if (y < eps)
                                EdgeX = x;
                        }
                }
                else
                    for (x = EdgeX; x > -eps; x -= dx)
                        if (Validate(x, y))
                        {
                            node[NodeNum].x = x;
                            node[NodeNum].y = y;
                            node[NodeNum].number = NodeNum;
                            NodeNum++;
                        }
                eo++;
            }

            //Определение типов узлов
            foreach (Node a in node)
                a.Type = GetType(a.x, a.y);

            //Определение соседей
            foreach (Node a in node)
            {
                foreach (Node b in node)
                {
                    if (Math.Abs(a.x + dx - b.x) < eps && Math.Abs(a.y - b.y) < eps)
                        a.numOfxPlus = b.number;

                    if (Math.Abs(a.x - dx - b.x) < eps && Math.Abs(a.y - b.y) < eps)
                        a.numOfxMinus = b.number;

                    if (Math.Abs(a.y + dy - b.y) < eps && Math.Abs(a.x - b.x) < eps)
                        a.numOfyPlus = b.number;

                    if (Math.Abs(a.y - dy - b.y) < eps && Math.Abs(a.x - b.x) < eps)
                        a.numOfyMinus = b.number;
                }
            }
            foreach (Node a in node)
            {
                foreach (Node b in node)
                {
                    if (a.numOfyMinus == b.number)
                    {
                        a.numOfDiag = b.numOfxMinus;
                        //   MessageBox.Show("A.Number:" + Convert.ToString(a.number) + ";   numOfy-:" + a.numOfyMinus.ToString() + ";  numOfDiagA: " + a.numOfDiag.ToString());
                    }
                }
            }
            //Проверка выполнения ограничений задачи
            foreach (Node a in node)
            {
                if ((a.Type == 0 && a.numOfxMinus < 0) && (a.numOfyMinus < 0 || a.numOfyPlus < 0 || a.numOfxPlus < 0))
                {
                    MessageBox.Show("Ограничение программы: шаг должен быть таким, чтобы узлы попадали на границу. Программа будет закрыта.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    Application.Exit();
                }
            }

            //Задание граничных и начальных условий
            foreach (Node a in node)
            {
                if (a.Type == 1)
                    a.t = 100;
                else if (a.Type == 2)
                    a.t = 40;
                else if (a.Type == 3)
                    a.t = 200;
                else if (a.Type == 4)
                    a.t = 100;
                else
                    a.t = 0;
            }

            //Инициализация пиксельного поля, переменных, рисование рамки
            pictureBox1.Image = btmFront;

            dxp = (int)System.Math.Round(dx * 150);
            dyp = (int)System.Math.Round(dy * 150);
            dxph = (int)System.Math.Round(dx * 150 / 2);
            dyph = (int)System.Math.Round(dy * 150 / 2);

            //Главный цикл
            int counter = 0; //Счетчик итераций
            double[,] m = new double[NodesNum, NodesNum + 1]; //Выделение памяти для матрицы коэффициентов

            do
            {
                InitMatrix(m, node); //Инициализация матрицы

                Gauss(m, node); //Обработка матрицы методом Гаусса, обновление температур в узлах

                counter++;
                label5.Text = "Шаг " + counter + " из " + steps;
                label5.Refresh();
                InitMatrix2(m, node);
                ResultDraw(node); //Рисование температурного распределения
                pictureBox1.Refresh(); //Обновление изображения
            }
            while (counter < steps);


            ResultDraw(node); //Рисование температурного распределения
            pictureBox1.Refresh(); //Обновление изображения

            //Снятие блокировки полей ввода
            textBox1.ReadOnly = false;
            textBox2.ReadOnly = false;
            textBox3.ReadOnly = false;
        }

        private class Node
        {
            public double x, y, t;
            public int number, Type;
            public int numOfyPlus = -1, numOfyMinus = -1, numOfxPlus = -1, numOfxMinus = -1, numOfDiag = -1;
        }

        private bool Validate(double x, double y)
        {
            bool flag = false;

            if (y <= 4 + eps && x + y - 8 <= eps && y >= -eps && x >= -eps)
                flag = true;
            else
                flag = false;

            return flag;
        }

        private int GetType(double x, double y)
        {
            int flag = 0;

            if (Math.Abs(y) <= eps || Math.Abs(y - 4) <= eps)
                flag = 1;
            else if (Math.Abs(x) <= eps)
                flag = 3;
            else if (Math.Abs(x + y - 8) <= eps && x >= 4)
                flag = 2;

            return flag;
        }

        private void InitMatrix(double[,] m, Node[] node)
        {
            for (int i = 0; i < NodesNum; i++)
                for (int j = 0; j <= NodesNum; j++)
                    m[i, j] = 0;
            int fff = 1;
            foreach (Node a in node)
            {
                if (a.Type == 0)
                {
                    m[a.number, a.numOfyMinus] = (-k * dt) / (dy * dy);
                    m[a.number, a.number] = 1 + 2 * k * dt / (dx * dx) + 2 * k * dt / (dy * dy);
                    m[a.number, a.numOfyPlus] = (-k * dt) / (dy * dy);

                    // if (a.Type == 0)
                    // {
                    m[a.number, a.numOfxMinus] = (-k * dt) / (dx * dx);
                    m[a.number, a.numOfxPlus] = (-k * dt) / (dx * dx);

                    // }
                    //else
                    // m[a.number, a.numOfxPlus] = (-2 * k * dt) / (dx * dx);

                    m[a.number, NodesNum] = a.t;
                }
                else if (a.Type == 1)
                {
                    m[a.number, a.number] = 1;
                    m[a.number, NodesNum] = a.t;
                }
                else if (a.Type == 2)
                {

                    m[a.number, a.number] = 1;

                    m[a.number, NodesNum] = m[a.numOfDiag, NodesNum] - 40 * 0.2 * Math.Sqrt(2);
                }




                else if (a.Type == 3)
                {
                    m[a.number, a.number] = 1;
                    m[a.number, NodesNum] = a.t;

                }
                else if (a.Type == 4)
                {

                    int val = 500;

                    m[a.number, a.numOfyMinus] = (-k * dt) / (dy * dy);// -val;
                    m[a.number, a.number] = 1 + 2 * k * dt / (dx * dx) + 2 * k * dt / (dy * dy);// +val / (dx * dx);// +val;
                    m[a.number, a.numOfyPlus] = (-k * dt) / (dy * dy);// +val;


                    m[a.number, a.numOfxMinus] = (-k * dt) / (dx * dx);// +val;
                    m[a.number, a.numOfxPlus] = (-k * dt) / (dx * dx);// +val;
                    m[a.number, NodesNum] = a.t;//+2000;

                }
            }
        }

        private void InitMatrix2(double[,] m, Node[] node)
        {

            for (int i = NodesNum - 1; i >= 0; i--)
            {
                if (node[i].Type == 2)
                {
                    node[i].t = node[node[i].numOfDiag].t - 40 * 0.2 * Math.Sqrt(2);
                }
            }
        }

        private void Gauss(double[,] m, Node[] node)
        {
            //Прямой ход
            double mult = 0;

            for (int p = 0; p < NodesNum - 1; p++)
            {
                for (int i = p + 1; i < NodesNum; i++)
                {
                    if (Math.Abs(m[i, p]) > eps)
                    {
                        mult = m[i, p] / m[p, p];

                        for (int j = NodesNum; j >= p; j--)
                        {
                            if (Math.Abs(m[p, j]) > eps)
                                m[i, j] -= m[p, j] * mult;
                        }
                    }
                }
            }



            //Обратный ход
            for (int i = NodesNum - 1; i >= 0; i--)
            {
                node[i].t = m[i, NodesNum];

                for (int j = NodesNum - 1; j > i; j--)
                    node[i].t -= m[i, j] * node[j].t;

                node[i].t /= m[i, i];

            }

        }

        private void ResultDraw(Node[] node)
        {
            //Рисование температурного распределения
            foreach (Node a in node)
            {
                //Преобразование координат
                xp = (int)Math.Round(150 * a.x + 20);
                yp = (int)Math.Round(640 - 150 * a.y - 20);

                //Определение цвета на основе температуры узла
                sat = 1.0;
                val = 1.0;
                hue = (double)0.66 - a.t / 326; //152;
                Graphics g = Graphics.FromImage(btmFront);

                double be = Math.Round(a.t, 2);
                temperature = Convert.ToString(be);
                numero = Convert.ToString(a.number);

                HSVtoRGB(hue, sat, val, out dr, out dg, out db);
                c = Color.FromArgb((int)(dr * 255), (int)(dg * 255), (int)(db * 255));


                //Рисование одноцветного прямоугольника
                for (int i = 0; i < dxp; i++)
                    for (int j = 0; j < dyp; j++)
                    { 
                        btmFront.SetPixel(xp - dxph + i, yp - dyph + j, c);
                    }
                if (checkBox1.Checked)
                {
                    g.DrawString(numero, new Font("Times New Roman", 7, FontStyle.Bold), Brushes.Black, new PointF(xp - dxph, yp - dyph), new StringFormat());
                    g.DrawString(temperature, new Font("Times New Roman", 7, FontStyle.Bold), Brushes.Black, new PointF(xp - dxph, yp), new StringFormat());
                }
            }
        }

        private void HSVtoRGB(double H, double S, double V, out double R, out double G, out double B)
        {
            if (H == 1.0)
                H = 0.0;

            double step = 1.0 / 6.0;
            double vh = H / step;
            int i = (int)Math.Floor(vh);
            double f = vh - i;
            double p = V * (1.0 - S);
            double q = V * (1.0 - (S * f));
            double t = V * (1.0 - (S * (1.0 - f)));

            switch (i)
            {
                case 0:
                    {
                        R = V;
                        G = t;
                        B = p;
                        break;
                    }

                case 1:
                    {
                        R = q;
                        G = V;
                        B = p;
                        break;
                    }

                case 2:
                    {
                        R = p;
                        G = V;
                        B = t;
                        break;
                    }

                case 3:
                    {
                        R = p;
                        G = q;
                        B = V;
                        break;
                    }

                case 4:
                    {
                        R = t;
                        G = p;
                        B = V;
                        break;
                    }

                case 5:
                    {
                        R = V;
                        G = p;
                        B = q;
                        break;
                    }

                default:
                    {
                        throw new ArgumentException();
                    }

            }
        }
    }
}
