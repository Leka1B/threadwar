using System;
using System.Runtime.InteropServices;
using System.Threading;

class Program
{
    // Платформенно-зависимый вызов
    [DllImport("kernel32.dll")]
    static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    static extern bool ReadConsoleInput(IntPtr hConsoleInput, [Out] INPUT_RECORD[] lpBuffer, uint nLength, out uint lpNumberOfEventsRead);

    [DllImport("kernel32.dll")]
    static extern bool WriteConsoleOutputCharacter(IntPtr hConsoleOutput, string lpCharacter, uint nLength, COORD dwWriteCoord, out uint lpNumberOfCharsWritten);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool ReadConsoleOutputCharacter(IntPtr hConsoleOutput, [Out] char[] lpCharacter, uint nLength, COORD dwReadCoord, out uint lpNumberOfCharsRead);

    [DllImport("kernel32.dll")]
    static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

    [DllImport("kernel32.dll")]
    static extern bool SetConsoleTitle(string lpConsoleTitle);

    // Константы
    const int STD_INPUT_HANDLE = -10;
    const int STD_OUTPUT_HANDLE = -11;

    // Структуры
    [StructLayout(LayoutKind.Sequential)]
    struct COORD
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT_RECORD
    {
        public ushort EventType;
        public KEY_EVENT_RECORD KeyEvent;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KEY_EVENT_RECORD
    {
        public bool bKeyDown;
        public ushort wRepeatCount;
        public ushort wVirtualKeyCode;
        public ushort wVirtualScanCode;
        public char UnicodeChar;
        public uint dwControlKeyState;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct CONSOLE_SCREEN_BUFFER_INFO
    {
        public COORD dwSize;
        public COORD dwCursorPosition;
        public ushort wAttributes;
        public SMALL_RECT srWindow;
        public COORD dwMaximumWindowSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SMALL_RECT
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    }

    // Переменные
    static Mutex screenlock = new Mutex();
    static Semaphore bulletsem = new Semaphore(3, 3);
    static ManualResetEvent startevt = new ManualResetEvent(false);
    static IntPtr conin = GetStdHandle(STD_INPUT_HANDLE);
    static IntPtr conout = GetStdHandle(STD_OUTPUT_HANDLE);
    static Thread mainthread = Thread.CurrentThread;
    static int hit = 0;
    static int miss = 0;
    static char[] badchar = { '-', '\\', '|', '/' }; // кадры для анимированных противников
    static CONSOLE_SCREEN_BUFFER_INFO info;

    static Random rand = new Random();


    static void Main(string[] args)
    {
        string title = $"ThreadWar - Hits: {hit}, Misses: {miss}";
        SetConsoleTitle(title);

        GetConsoleScreenBufferInfo(conout, out info);
        int y = info.dwSize.Y - 1;
        int x = info.dwSize.X / 2;

        // Запуск потока для создания противников
        Thread badguysThread = new Thread(new ThreadStart(Badguys));
        badguysThread.Start();

        while (true)
        {
            int c, ct;
            WriteAt(x, y, '|'); // нарисовать пушку 
            c = GetAKey(out ct); // ввод

            switch (c)
            {
                case 32: // Пробел
                    COORD xy = new COORD { X = (short)x, Y = (short)y };
                    Thread bulletThread = new Thread(new ParameterizedThreadStart(Bullet));
                    bulletThread.Start(xy);
                    Thread.Sleep(100);
                    break;
                case 37: // Влево
                    startevt.Set();
                    WriteAt(x, y, ' ');
                    while (ct-- > 0)
                        if (x > 0) x--;
                    break;
                case 39: // Вправо
                    startevt.Set();
                    WriteAt(x, y, ' ');
                    while (ct-- > 0)
                        if (x < info.dwSize.X - 1) x++;
                    break;
            }
        }
    }

    static void WriteAt(int x, int y, char c)
    {
        screenlock.WaitOne();
        COORD pos = new COORD { X = (short)x, Y = (short)y };
        uint res;
        WriteConsoleOutputCharacter(conout, c.ToString(), 1, pos, out res);
        screenlock.ReleaseMutex();
    }

    static int GetAKey(out int repeatCount)
    {
        INPUT_RECORD[] record = new INPUT_RECORD[1];
        uint eventsRead;
        while (true)
        {
            ReadConsoleInput(conin, record, 1, out eventsRead);
            if (record[0].EventType != 1) continue; 
            if (!record[0].KeyEvent.bKeyDown) continue;
            repeatCount = record[0].KeyEvent.wRepeatCount;
            return record[0].KeyEvent.wVirtualKeyCode;
        }
    }

    static char GetAt(int x, int y)
    {
        char[] c = new char[1];
        COORD org = new COORD { X = (short)x, Y = (short)y };
        screenlock.WaitOne();
        uint res;
        ReadConsoleOutputCharacter(conout, c, 1, org, out res);
        screenlock.ReleaseMutex();
        return c[0];
    }

    static void Score()
    {
        string title = $"ThreadWar - Hits: {hit}, Misses: {miss}";
        SetConsoleTitle(title);
        if (miss >= 30)
        {
            Thread.Sleep(Timeout.Infinite);
            Console.WriteLine("Игра окончена!");
            Environment.Exit(0);
        }
    }

    static void Badguy(object yObj)
    {
        int y = (int)yObj;
        int dir;
        int x = y % 2 == 0 ? info.dwSize.X : 0;
        dir = x == 0 ? 1 : -1;

        while ((dir == 1 && x != info.dwSize.X) || (dir == -1 && x != 0))
        {
            bool hitme = false;
            WriteAt(x, y, badchar[x % 4]);

            for (int i = 0; i < 15; i++)
            {
                Thread.Sleep(40);
                if (GetAt(x, y) == '*')
                {
                    hitme = true;
                    break;
                }
            }
            WriteAt(x, y, ' ');

            if (hitme)
            {
                Console.Beep();
                Interlocked.Increment(ref hit);
                Score();
                return;
            }
            x += dir;
        }
        Interlocked.Increment(ref miss);
        Score();
    }

    static void Badguys()
    {
        startevt.WaitOne(15000);
        while (true)
        {
            if (rand.Next(0, 100) < (hit + miss) / 25 + 20)
            {
                int y = rand.Next(1, 10);
                Thread badguyThread = new Thread(new ParameterizedThreadStart(Badguy));
                badguyThread.Start(y);
            }
            Thread.Sleep(1000);
        }
    }

    static void Bullet(object xyObj)
    {
        COORD xy = (COORD)xyObj;
        if (GetAt(xy.X, xy.Y) == '*') return;

        if (!bulletsem.WaitOne(0)) return;

        while (--xy.Y > 0)
        {
            WriteAt(xy.X, xy.Y, '*');
            Thread.Sleep(100);
            WriteAt(xy.X, xy.Y, ' ');
        }

        bulletsem.Release();
    }
}
