; ============================================================
; Подготовка спрайтов домов для изометрической игры
; Холст 256×256, центр пола на точке (128, 200)
;
; Запуск: Фильтры → Script-Fu → Консоль
;         Вставить содержимое и нажать Выполнить
; ============================================================

(define (prepare-sprite input-path output-path scaled-w scaled-h floor-y-in-sprite)
  (let* (
    (image    (car (gimp-file-load RUN-NONINTERACTIVE input-path input-path)))
    (layer    (car (gimp-image-get-active-drawable image)))
    (canvas-w 256)
    (canvas-h 256)
    (target-floor-y 200)   ; пол должен быть на y=200 в финальном холсте
  )

    ; 1. Масштабируем изображение
    (gimp-image-scale-full image scaled-w scaled-h INTERPOLATION-LINEAR)

    ; 2. Вычисляем смещения:
    ;    по X — центрируем
    ;    по Y — так, чтобы центр пола оказался на target-floor-y
    (let* (
      (offset-x (/ (- canvas-w scaled-w) 2))
      (offset-y (- target-floor-y floor-y-in-sprite))
    )

      ; 3. Расширяем холст до 256×256
      (gimp-image-resize image canvas-w canvas-h offset-x offset-y)

      ; 4. Расширяем слой на весь холст (прозрачные края сохраняются)
      (gimp-layer-resize-to-image-size (car (gimp-image-get-active-drawable image)))

      ; 5. Экспортируем PNG
      (file-png-save RUN-NONINTERACTIVE
        image
        (car (gimp-image-get-active-drawable image))
        output-path output-path
        0    ; interlace
        9    ; compression
        1 1 1 1 1)

      (gimp-image-delete image)
      (gimp-message (string-append "Готово: " output-path))
    )
  )
)

; ============================================================
; Запуск для обоих спрайтов
;
; Параметры: путь, путь-выхода, ширина, высота, Y-центра-пола-в-масштабированном-спрайте
;
; floor-y-in-sprite — это где находится центр ромба пола
; в масштабированном изображении (до вставки в 256×256).
; Визуально: примерно 80% высоты спрайта (пол внизу).
; ============================================================

; house_level1: масштаб 134×134, центр пола ≈ y=115 в масштабированном
(prepare-sprite
  "/Users/kemal/новая-бронзовая-игра-/Assets/Buildings/house_level1.png"
  "/Users/kemal/новая-бронзовая-игра-/Assets/Buildings/house_level1.png"
  134 134
  115)

; house_level2: масштаб 164×164, центр пола ≈ y=138 в масштабированном
(prepare-sprite
  "/Users/kemal/новая-бронзовая-игра-/Assets/Buildings/house_level2.png"
  "/Users/kemal/новая-бронзовая-игра-/Assets/Buildings/house_level2.png"
  164 164
  138)
