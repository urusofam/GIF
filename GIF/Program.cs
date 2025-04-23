using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Accord.Video.FFMPEG; // Оставляем для чтения видео

namespace VideoToGifConverter
{
    public partial class MainForm : Form
    {
        private string sourceVideoPath = "";
        private string outputGifPath = "";
        private VideoFileReader videoReader = null; // Для чтения кадров из видео
        private List<Bitmap> frames = new List<Bitmap>(); // Список кадров для GIF
        private int frameSkip = 2; // Берем каждый второй кадр по умолчанию
        private int gifWidth = 320; // Размер GIF по умолчанию
        private int gifHeight = 240;
        private int frameDelay = 10; // Задержка между кадрами в сотых долях секунды (10 = 100ms)
        // Поле для хранения потока памяти для отображаемого GIF
        private MemoryStream currentPreviewStream = null;


        public MainForm()
        {
            InitializeComponent(); // Метод, генерируемый дизайнером форм (если есть)
            InitializeControls(); // Настройка элементов управления вручную
        }

        // Инициализация элементов управления формы
        private void InitializeControls()
        {
            this.Text = "Video to GIF Converter";
            this.Width = 600;
            this.Height = 500;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // --- Элементы управления ---

            // Кнопка выбора видео
            Button btnSelectVideo = new Button
            {
                Text = "Выбрать видео",
                Location = new Point(20, 20),
                Width = 150,
                Height = 30,
                Font = new Font("Segoe UI", 9F)
            };
            btnSelectVideo.Click += BtnSelectVideo_Click;
            this.Controls.Add(btnSelectVideo);

            // Текстовое поле для пути к видео
            TextBox txtVideoPath = new TextBox
            {
                Name = "txtVideoPath",
                Location = new Point(190, 20),
                Width = 370,
                Height = 30,
                ReadOnly = true,
                Font = new Font("Segoe UI", 9F)
            };
            this.Controls.Add(txtVideoPath);

            // Метка "Пропуск кадров"
            Label lblFrameSkip = new Label
            {
                Text = "Пропуск кадров:",
                Location = new Point(20, 70),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };
            this.Controls.Add(lblFrameSkip);

            // Поле для ввода пропуска кадров
            NumericUpDown nudFrameSkip = new NumericUpDown
            {
                Name = "nudFrameSkip",
                Location = new Point(150, 68), // Выровняем по вертикали
                Width = 60,
                Height = 23, // Стандартная высота
                Minimum = 1,
                Maximum = 10,
                Value = frameSkip,
                Font = new Font("Segoe UI", 9F)
            };
            nudFrameSkip.ValueChanged += (sender, e) => frameSkip = (int)nudFrameSkip.Value;
            this.Controls.Add(nudFrameSkip);

            // Метка "Размер GIF"
            Label lblGifSize = new Label
            {
                Text = "Размер GIF:",
                Location = new Point(250, 70),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };
            this.Controls.Add(lblGifSize);

            // Поле для ввода ширины GIF
            NumericUpDown nudGifWidth = new NumericUpDown
            {
                Name = "nudGifWidth",
                Location = new Point(340, 68),
                Width = 60,
                Height = 23,
                Minimum = 80,
                Maximum = 1280,
                Value = gifWidth,
                Increment = 40,
                Font = new Font("Segoe UI", 9F)
            };
            nudGifWidth.ValueChanged += (sender, e) => gifWidth = (int)nudGifWidth.Value;
            this.Controls.Add(nudGifWidth);

            // Метка "x"
            Label lblX = new Label
            {
                Text = "x",
                Location = new Point(410, 70),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9F)
            };
            this.Controls.Add(lblX);

            // Поле для ввода высоты GIF
            NumericUpDown nudGifHeight = new NumericUpDown
            {
                Name = "nudGifHeight",
                Location = new Point(430, 68),
                Width = 60,
                Height = 23,
                Minimum = 60,
                Maximum = 720,
                Value = gifHeight,
                Increment = 30,
                Font = new Font("Segoe UI", 9F)
            };
            nudGifHeight.ValueChanged += (sender, e) => gifHeight = (int)nudGifHeight.Value;
            this.Controls.Add(nudGifHeight);

            // Метка "Задержка кадра"
            Label lblFrameDelay = new Label
            {
                Text = "Задержка (1/100 с):", // Уточняем единицы
                Location = new Point(20, 110),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };
            this.Controls.Add(lblFrameDelay);

            // Поле для ввода задержки кадра
            NumericUpDown nudFrameDelay = new NumericUpDown
            {
                Name = "nudFrameDelay",
                Location = new Point(150, 108),
                Width = 60,
                Height = 23,
                Minimum = 1, // 1/100 секунды (10 мс)
                Maximum = 100, // 1 секунда
                Value = frameDelay,
                Font = new Font("Segoe UI", 9F)
            };
            nudFrameDelay.ValueChanged += (sender, e) => frameDelay = (int)nudFrameDelay.Value;
            this.Controls.Add(nudFrameDelay);

            // Кнопка конвертации
            Button btnConvert = new Button
            {
                Name = "btnConvert",
                Text = "Конвертировать в GIF",
                Location = new Point(20, 150),
                Width = 170,
                Height = 30,
                Enabled = false, // Изначально неактивна
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnConvert.Click += BtnConvert_Click;
            this.Controls.Add(btnConvert);

            // Область предпросмотра
            PictureBox previewBox = new PictureBox
            {
                Name = "previewBox",
                Location = new Point(20, 200),
                Width = 540,
                Height = 240,
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.LightGray // Фон для наглядности
            };
            this.Controls.Add(previewBox);

            // Прогресс-бар
            ProgressBar progressBar = new ProgressBar
            {
                Name = "progressBar",
                Location = new Point(200, 150),
                Width = 360,
                Height = 30,
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };
            this.Controls.Add(progressBar);
        }

        // Обработчик нажатия кнопки "Выбрать видео"
        private void BtnSelectVideo_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Video files (*.mp4;*.avi;*.mkv;*.mov;*.wmv)|*.mp4;*.avi;*.mkv;*.mov;*.wmv|All files (*.*)|*.*";
                openFileDialog.RestoreDirectory = true;
                openFileDialog.Title = "Выберите видеофайл";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    sourceVideoPath = openFileDialog.FileName;
                    ((TextBox)this.Controls["txtVideoPath"]).Text = sourceVideoPath;
                    ((Button)this.Controls["btnConvert"]).Enabled = true;

                    // Попытка показать первый кадр для предпросмотра
                    ShowPreviewFrame();
                }
            }
        }

        // Показывает первый кадр видео в PictureBox
        private void ShowPreviewFrame()
        {
            if (string.IsNullOrEmpty(sourceVideoPath) || !File.Exists(sourceVideoPath)) return;

            // Очищаем предыдущий предпросмотр (включая MemoryStream)
            ClearPreview();

            try
            {
                // Освобождаем предыдущий ридер, если он был
                if (videoReader != null)
                {
                    videoReader.Dispose();
                    videoReader = null;
                }

                videoReader = new VideoFileReader();
                videoReader.Open(sourceVideoPath);

                if (videoReader.FrameCount > 0)
                {
                    Bitmap previewFrame = videoReader.ReadVideoFrame();
                    if (previewFrame != null)
                    {
                        PictureBox previewBox = (PictureBox)this.Controls["previewBox"];
                        // Показываем измененный размер кадра
                        // Для статичного кадра можно не использовать MemoryStream
                        previewBox.Image = ResizeImage(previewFrame, previewBox.Width, previewBox.Height);
                        previewFrame.Dispose(); // Освобождаем оригинал
                    }
                }
                videoReader.Close(); // Закрываем сразу после чтения первого кадра
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось прочитать первый кадр видео: {ex.Message}", "Ошибка предпросмотра", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                // Очищаем предпросмотр в случае ошибки
                ClearPreview();
            }
            finally
            {
                // Убедимся, что ридер закрыт
                if (videoReader != null && videoReader.IsOpen)
                {
                    videoReader.Close();
                }
            }
        }


        // Обработчик нажатия кнопки "Конвертировать в GIF"
        private async void BtnConvert_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(sourceVideoPath) || !File.Exists(sourceVideoPath))
            {
                MessageBox.Show("Видеофайл не выбран или не найден.", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "GIF Image|*.gif";
                saveFileDialog.Title = "Сохранить GIF как";
                saveFileDialog.DefaultExt = "gif";
                saveFileDialog.AddExtension = true;
                saveFileDialog.FileName = Path.GetFileNameWithoutExtension(sourceVideoPath) + ".gif"; // Предлагаем имя файла

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    outputGifPath = saveFileDialog.FileName;
                    EnableControls(false); // Блокируем интерфейс
                    ClearPreview(); // Очищаем предпросмотр перед началом

                    try
                    {
                        // Запускаем конвертацию в фоновом потоке
                        await Task.Run(() => ConvertVideoToGifProcess());
                        MessageBox.Show($"GIF успешно создан и сохранен в:\n{outputGifPath}", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        // Показываем созданный GIF (если возможно)
                        ShowGeneratedGif(outputGifPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка во время конвертации: {ex.Message}\n\n{ex.StackTrace}", "Ошибка конвертации", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        // Очищаем предпросмотр в случае ошибки
                        ClearPreview();
                    }
                    finally
                    {
                        EnableControls(true); // Разблокируем интерфейс
                        // Очищаем список кадров в памяти
                        DisposeFrames();
                        // Сбрасываем прогресс бар
                        UpdateProgress(0, 100);
                    }
                }
            }
        }

        // Отображает созданный GIF в PictureBox, используя MemoryStream
        private void ShowGeneratedGif(string gifPath)
        {
            // Очищаем предыдущий предпросмотр (включая MemoryStream)
            ClearPreview();

            try
            {
                PictureBox previewBox = (PictureBox)this.Controls["previewBox"];

                // 1. Читаем все байты файла в массив
                byte[] gifBytes = File.ReadAllBytes(gifPath);

                // 2. Создаем MemoryStream из массива байт
                currentPreviewStream = new MemoryStream(gifBytes);

                // 3. Создаем Image из MemoryStream
                // MemoryStream НЕ нужно закрывать с помощью using,
                // так как он должен оставаться открытым для анимации Image
                previewBox.Image = Image.FromStream(currentPreviewStream);

            }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"Файл GIF не найден: {gifPath}");
                ClearPreview(); // Очищаем, если файла нет
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Не удалось отобразить GIF: {ex.Message}");
                // Если не удалось загрузить GIF, очищаем предпросмотр
                ClearPreview();
                // Освобождаем MemoryStream, если он был создан, но вызвал ошибку
                currentPreviewStream?.Dispose();
                currentPreviewStream = null;
            }
        }

        // Очищает PictureBox и освобождает связанный MemoryStream
        private void ClearPreview()
        {
            PictureBox previewBox = (PictureBox)this.Controls["previewBox"];
            if (previewBox.Image != null)
            {
                previewBox.Image.Dispose();
                previewBox.Image = null;
            }
            // Освобождаем предыдущий MemoryStream, если он был
            if (currentPreviewStream != null)
            {
                currentPreviewStream.Dispose();
                currentPreviewStream = null;
            }
        }


        // Основной процесс конвертации видео в GIF
        private void ConvertVideoToGifProcess()
        {
            DisposeFrames(); // Очищаем предыдущие кадры

            try
            {
                // --- 1. Чтение кадров из видео ---
                if (videoReader != null) videoReader.Dispose(); // Закрываем предыдущий, если был
                videoReader = new VideoFileReader();
                videoReader.Open(sourceVideoPath);

                int frameCount = (int)videoReader.FrameCount;
                int totalFramesToProcess = (frameCount + frameSkip - 1) / frameSkip; // Округление вверх
                if (totalFramesToProcess <= 0) totalFramesToProcess = 1;

                UpdateProgress(0, totalFramesToProcess); // Устанавливаем максимум прогресс-бара

                int processedFramesCount = 0;
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    Bitmap frame = videoReader.ReadVideoFrame();
                    if (frame != null)
                    {
                        if (frameIndex % frameSkip == 0)
                        {
                            // Изменяем размер кадра и добавляем в список
                            Bitmap resizedFrame = ResizeImage(frame, gifWidth, gifHeight);
                            frames.Add(resizedFrame); // Добавляем в список

                            processedFramesCount++;
                            // Обновляем прогресс-бар (только чтение кадров)
                            UpdateProgress(processedFramesCount, totalFramesToProcess);
                        }
                        frame.Dispose(); // Освобождаем оригинальный кадр
                    }
                }
                videoReader.Close();
                videoReader.Dispose();
                videoReader = null;

                // --- 2. Создание GIF из кадров ---
                if (frames.Count > 0)
                {
                    // Сбрасываем прогресс для этапа сохранения GIF
                    UpdateProgress(0, frames.Count);
                    CreateGifNative(frames, outputGifPath, frameDelay);
                }
                else
                {
                    throw new Exception("Не удалось прочитать кадры из видеофайла.");
                }

            }
            catch (Exception)
            {
                // Перебрасываем исключение, чтобы оно было поймано в BtnConvert_Click
                throw;
            }
            finally
            {
                // Убедимся, что ридер закрыт и ресурсы освобождены
                if (videoReader != null)
                {
                    if (videoReader.IsOpen) videoReader.Close();
                    videoReader.Dispose();
                    videoReader = null;
                }
                // Не очищаем frames здесь, они нужны для показа GIF после завершения
                // Освобождение кадров теперь происходит в DisposeFrames()
            }
        }

        // Метод для создания GIF-анимации с использованием System.Drawing
        private void CreateGifNative(List<Bitmap> images, string outputPath, int delayCentiseconds)
        {
            if (images == null || images.Count == 0)
                throw new ArgumentNullException("Список изображений пуст.");

            // Получаем стандартный кодек для GIF
            ImageCodecInfo gifEncoder = GetEncoder(ImageFormat.Gif);
            if (gifEncoder == null)
                throw new NotSupportedException("Кодек для GIF не найден в системе.");

            // --- Подготовка PropertyItem для задержки ---
            PropertyItem propItemDelay = null;
            try
            {
                // Пытаемся получить PropertyItem из первого кадра как шаблон
                // Это может вызвать исключение, если у Bitmap нет PropertyItems
                propItemDelay = images[0].PropertyItems.FirstOrDefault();
            }
            catch { } // Игнорируем исключение, если не удалось получить

            if (propItemDelay == null)
            {
                // Если не удалось получить или его нет, создаем вручную
                propItemDelay = (PropertyItem)System.Runtime.Serialization.FormatterServices
                                   .GetUninitializedObject(typeof(PropertyItem));
                propItemDelay.Id = 0x5100; // PropertyTagFrameDelay
                propItemDelay.Type = 4; // Массив 32-битных целых чисел (PropertyTagTypeLong)
                                        // propItemDelay.Len и propItemDelay.Value будут установлены ниже
            }

            byte[] delayBytes = new byte[images.Count * 4];
            int delayValue = Math.Max(1, delayCentiseconds); // Задержка не может быть 0, минимум 1/100 сек
            for (int i = 0; i < images.Count; i++)
            {
                // Копируем значение задержки (в 1/100 секунды) в массив байт
                BitConverter.GetBytes(delayValue).CopyTo(delayBytes, i * 4);
            }
            propItemDelay.Len = delayBytes.Length;
            propItemDelay.Value = delayBytes;

            // --- Сохранение GIF ---
            EncoderParameters encoderParams = new EncoderParameters(1);
            Bitmap firstFrame = images[0];

            try
            {
                firstFrame.SetPropertyItem(propItemDelay); // Устанавливаем задержку для всех кадров

                // Сохраняем первый кадр с флагом начала многокадрового файла
                encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
                firstFrame.Save(outputPath, gifEncoder, encoderParams);

                // Добавляем остальные кадры
                encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);
                for (int i = 1; i < images.Count; i++)
                {
                    firstFrame.SaveAdd(images[i], encoderParams);
                    UpdateProgress(i + 1, images.Count); // Обновляем прогресс сохранения
                }

                // Завершаем сохранение многокадрового файла
                encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.Flush);
                firstFrame.SaveAdd(encoderParams);
            }
            catch (Exception ex)
            {
                // Оборачиваем исключение для предоставления контекста
                throw new Exception($"Ошибка при сохранении GIF файла '{outputPath}': {ex.Message}", ex);
            }
            finally
            {
                // Освобождаем параметры кодировщика
                encoderParams?.Dispose();
            }

            // Важно: Не освобождаем кадры здесь, если они нужны для показа в previewBox
            // Освобождение происходит в DisposeFrames() или при закрытии формы
        }


        // Вспомогательный метод для получения кодека изображения
        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            return ImageCodecInfo.GetImageEncoders().FirstOrDefault(codec => codec.FormatID == format.Guid);
        }


        // Изменяет размер изображения с сохранением качества
        private Bitmap ResizeImage(Bitmap image, int width, int height)
        {
            if (image == null) return null;

            // Создаем новый Bitmap с нужными размерами и форматом, поддерживающим альфа-канал
            Bitmap destImage = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            // Устанавливаем разрешение такое же, как у исходного изображения
            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (Graphics graphics = Graphics.FromImage(destImage))
            {
                // Устанавливаем высокое качество рендеринга
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                // Используем ImageAttributes для предотвращения артефактов по краям
                using (ImageAttributes wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                    // Рисуем исходное изображение на новом с изменением размера
                    graphics.DrawImage(image, new Rectangle(0, 0, width, height),
                                       0, 0, image.Width, image.Height,
                                       GraphicsUnit.Pixel, wrapMode);
                }
            }
            return destImage;
        }

        // Включает или отключает элементы управления на форме
        private void EnableControls(bool enabled)
        {
            // Используем Invoke для безопасного доступа к элементам управления из другого потока
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { EnableControlsInternal(enabled); });
            }
            else
            {
                EnableControlsInternal(enabled);
            }
        }

        private void EnableControlsInternal(bool enabled)
        {
            foreach (Control control in this.Controls)
            {
                // Не блокируем ProgressBar и саму форму
                if (control is ProgressBar || control == this) continue;
                control.Enabled = enabled;
            }
            // Управляем кнопкой конвертации отдельно (она должна быть активна только если выбран файл)
            if (enabled && !string.IsNullOrEmpty(sourceVideoPath) && File.Exists(sourceVideoPath))
            {
                ((Button)this.Controls["btnConvert"]).Enabled = true;
            }
            else if (enabled) // Если включаем, но файл не выбран
            {
                ((Button)this.Controls["btnConvert"]).Enabled = false;
            }
            // Если выключаем (enabled == false), кнопка уже заблокирована циклом выше

            this.UseWaitCursor = !enabled; // Показываем курсор ожидания во время работы
        }

        // Обновляет значение ProgressBar
        private void UpdateProgress(int value, int maximum)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { UpdateProgressInternal(value, maximum); });
            }
            else
            {
                UpdateProgressInternal(value, maximum);
            }
        }

        private void UpdateProgressInternal(int value, int maximum)
        {
            try
            {
                ProgressBar progressBar = (ProgressBar)this.Controls["progressBar"];
                // Устанавливаем максимум ПЕРЕД значением
                if (maximum > 0 && progressBar.Maximum != maximum)
                {
                    progressBar.Maximum = maximum;
                }
                // Корректируем значение, чтобы оно было в пределах [Minimum, Maximum]
                if (value < progressBar.Minimum) value = progressBar.Minimum;
                if (value > progressBar.Maximum) value = progressBar.Maximum;

                progressBar.Value = value;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обновления прогресс-бара: {ex.Message}");
            }
        }

        // Освобождает ресурсы Bitmap из списка frames
        private void DisposeFrames()
        {
            if (frames != null)
            {
                foreach (Bitmap frame in frames)
                {
                    frame?.Dispose();
                }
                frames.Clear();
            }
        }

        // Освобождение ресурсов при закрытии формы
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            // Освобождаем видео ридер, если он открыт
            if (videoReader != null)
            {
                if (videoReader.IsOpen) videoReader.Close();
                videoReader.Dispose();
                videoReader = null;
            }
            // Освобождаем все кадры в памяти
            DisposeFrames();
            // Освобождаем изображение и MemoryStream в предпросмотре
            ClearPreview();
        }
    }

    // Класс Program и пустой InitializeComponent для запуска формы
    // (Обычно генерируется автоматически)
    public partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            // Дополнительно освобождаем неуправляемые ресурсы (если есть)
            // и ресурсы, созданные вручную (кадры, ридер, stream)
            if (disposing)
            {
                DisposeFrames();
                if (videoReader != null)
                {
                    if (videoReader.IsOpen) videoReader.Close();
                    videoReader.Dispose();
                    videoReader = null;
                }
                ClearPreview(); // Освобождает Image и MemoryStream
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 461); // Размер окна по умолчанию
            this.Name = "MainForm";
            this.Text = "Video to GIF Converter"; // Заголовок окна
            this.ResumeLayout(false);

        }

        #endregion
    }

    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
