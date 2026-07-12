from PIL import Image, ImageDraw
import argparse
import random

RESOLUTIONS = {
    "FHD": (1920, 1080),
    "QHD": (2560, 1440),
    "4K": (3840, 2160),
}
DEFAULT_RESOLUTION = "FHD"
WIDTH, HEIGHT = RESOLUTIONS[DEFAULT_RESOLUTION]

STAR_COLORS = [
    (25, 70, 160),    # bleu plus fonce
    (135, 45, 22),    # rouge plus fonce
    (255, 245, 210),  # jaune plus pale
    (255, 255, 255),  # blanc
]

COLOR_MODE_COLOR = "color"
COLOR_MODE_GRAYSCALE = "gray"
DEFAULT_COLOR_MODE = COLOR_MODE_COLOR
MIN_STAR_RADIUS = 0.5
MAX_STAR_RADIUS = 4.5
STAR_SIZE_DISTRIBUTION_POWER = 3.8
DEFAULT_STAR_COUNT = 400
ANTIALIAS_SCALE = 4
DEFAULT_FILENAME = "starfield.png"
DEFAULT_ROTATED_FILENAME = "starfield_rotated.png"


def get_resample_filter():
    resampling = getattr(Image, "Resampling", None)
    if resampling is not None:
        return resampling.LANCZOS
    return getattr(Image, "LANCZOS", 1)


def get_rotation_resample_filter():
    resampling = getattr(Image, "Resampling", None)
    if resampling is not None:
        return resampling.BICUBIC
    return getattr(Image, "BICUBIC", 3)


def random_star_radius(size_distribution_power):
    return MIN_STAR_RADIUS + (random.random() ** size_distribution_power) * (
        MAX_STAR_RADIUS - MIN_STAR_RADIUS
    )


def brightness_from_radius(radius):
    size_ratio = (radius - MIN_STAR_RADIUS) / (MAX_STAR_RADIUS - MIN_STAR_RADIUS)
    brightness = 0.25 + size_ratio * 0.75
    return min(1.0, brightness * random.uniform(0.9, 1.0))


def to_grayscale(color):
    r, g, b = color
    gray = int((0.299 * r) + (0.587 * g) + (0.114 * b))
    return gray, gray, gray


def choose_star_color(color_mode):
    color = random.choice(STAR_COLORS)
    if color_mode == COLOR_MODE_GRAYSCALE:
        return to_grayscale(color)
    return color


def add_star(draw, x, y, radius, color, brightness):
    r, g, b = color
    star_color = (
        int(r * brightness),
        int(g * brightness),
        int(b * brightness),
    )

    draw.ellipse(
        (x - radius, y - radius, x + radius, y + radius),
        fill=star_color,
    )


def background_color_for_mode(color_mode):
    return (0, 0, 8) if color_mode == COLOR_MODE_COLOR else (3, 3, 3)


def generate_starfield(
    filename=DEFAULT_FILENAME,
    star_count=DEFAULT_STAR_COUNT,
    size_distribution_power=STAR_SIZE_DISTRIBUTION_POWER,
    color_mode=DEFAULT_COLOR_MODE,
    width=WIDTH,
    height=HEIGHT,
):
    render_width = width * ANTIALIAS_SCALE
    render_height = height * ANTIALIAS_SCALE
    background_color = background_color_for_mode(color_mode)
    img = Image.new("RGB", (render_width, render_height), background_color)
    draw = ImageDraw.Draw(img)

    for _ in range(star_count):
        x = random.uniform(0, width - 1) * ANTIALIAS_SCALE
        y = random.uniform(0, height - 1) * ANTIALIAS_SCALE
        radius = random_star_radius(size_distribution_power)
        brightness = brightness_from_radius(radius)
        color = choose_star_color(color_mode)

        add_star(draw, x, y, radius * ANTIALIAS_SCALE, color, brightness)

    img = img.resize((width, height), get_resample_filter())
    img.save(filename)
    print(f"Image generee : {filename}")
    return img


def rotate_starfield(img, rotation_degrees, filename, color_mode):
    rotated = img.rotate(
        rotation_degrees,
        resample=get_rotation_resample_filter(),
        expand=False,
        fillcolor=background_color_for_mode(color_mode),
    )
    rotated.save(filename)
    print(f"Image tournee generee : {filename} ({rotation_degrees} deg)")
    return rotated


def parse_args():
    parser = argparse.ArgumentParser(
        description="Genere une image de champ d'etoiles."
    )
    parser.add_argument(
        "-n",
        "--star-count",
        type=int,
        default=DEFAULT_STAR_COUNT,
        help=f"nombre d'etoiles a generer (defaut: {DEFAULT_STAR_COUNT})",
    )
    parser.add_argument(
        "-p",
        "--size-distribution-power",
        type=float,
        default=STAR_SIZE_DISTRIBUTION_POWER,
        help=(
            "puissance de distribution des tailles; plus elle est elevee, "
            f"plus les petites etoiles dominent (defaut: {STAR_SIZE_DISTRIBUTION_POWER})"
        ),
    )
    parser.add_argument(
        "-m",
        "--color-mode",
        choices=(COLOR_MODE_COLOR, COLOR_MODE_GRAYSCALE),
        default=DEFAULT_COLOR_MODE,
        help=(
            "mode de couleur: color ou gray "
            f"(defaut: {DEFAULT_COLOR_MODE})"
        ),
    )
    parser.add_argument(
        "-r",
        "--resolution",
        choices=tuple(RESOLUTIONS),
        default=DEFAULT_RESOLUTION,
        help=(
            "resolution de l'image: FHD, QHD ou 4K "
            f"(defaut: {DEFAULT_RESOLUTION})"
        ),
    )
    parser.add_argument(
        "--rotation",
        type=float,
        default=None,
        help=(
            "angle de rotation en degres; si fourni, genere aussi "
            f"{DEFAULT_ROTATED_FILENAME}"
        ),
    )
    parser.add_argument(
        "--output",
        default=DEFAULT_FILENAME,
        help=f"nom du fichier image de reference (defaut: {DEFAULT_FILENAME})",
    )
    parser.add_argument(
        "--rotated-output",
        default=DEFAULT_ROTATED_FILENAME,
        help=(
            "nom du fichier image tournee "
            f"(defaut: {DEFAULT_ROTATED_FILENAME})"
        ),
    )
    return parser.parse_args()


if __name__ == "__main__":
    args = parse_args()
    width, height = RESOLUTIONS[args.resolution]
    img = generate_starfield(
        filename=args.output,
        star_count=args.star_count,
        size_distribution_power=args.size_distribution_power,
        color_mode=args.color_mode,
        width=width,
        height=height,
    )
    if args.rotation is not None:
        rotate_starfield(
            img,
            args.rotation,
            args.rotated_output,
            args.color_mode,
        )
