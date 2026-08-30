from PIL import Image, ImageDraw, ImageFont
import os

def create_icon(path):
    # Sizes standard for Windows icons
    sizes = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)]
    images = []

    for size in sizes:
        w, h = size
        # Create a new image with transparency
        img = Image.new('RGBA', size, (0, 0, 0, 0))
        draw = ImageDraw.Draw(img)

        # 1. Background (Rounded Rect / Circle)
        # Deep Blue/Purple gradient-ish solid color
        bg_color = (40, 44, 52, 255) # Dark gray/blue
        accent_color = (97, 175, 239, 255) # Light blue
        
        # Draw rounded rectangle (container)
        padding = w // 8
        draw.rounded_rectangle(
            [(padding, padding), (w - padding, h - padding)],
            radius=w//4,
            fill=bg_color,
            outline=accent_color,
            width=w//16
        )

        # 2. Camera Lens / Shutter center
        center = (w // 2, h // 2)
        radius = w // 4
        draw.ellipse(
            [(center[0]-radius, center[1]-radius), (center[0]+radius, center[1]+radius)],
            outline=accent_color,
            width=w//20
        )

        # 3. "OCR" lines inside the lens
        line_w = w // 3
        line_h = w // 16
        spacing = w // 10
        
        # Line 1
        draw.rectangle(
            [(center[0] - line_w//2, center[1] - spacing), 
             (center[0] + line_w//2, center[1] - spacing + line_h)],
            fill=(255, 255, 255, 200)
        )
        # Line 2
        draw.rectangle(
            [(center[0] - line_w//2, center[1] + spacing - line_h), 
             (center[0] + line_w//2, center[1] + spacing)],
            fill=(255, 255, 255, 200)
        )
        
        images.append(img)

    # Save as ICO
    images[0].save(path, format='ICO', sizes=sizes)
    print(f"Icon saved to {path}")

if __name__ == "__main__":
    create_icon(r"C:\Users\mbula\Projects\Repo\AutoCapture-OCR\dev\App\app.ico")
