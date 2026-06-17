# _*_ coding: utf-8 _*_
# Accuracy test script – builds on game_test.py
# Displays random circles, records gaze vs target distance to CSV.

import csv
import math
import os
import re
import random
import time

import pygame
from pygame.locals import KEYDOWN, K_RETURN, K_c, K_s, K_SPACE, K_ESCAPE

from gazefollower import GazeFollower
from gazefollower.gaze_estimator import MGazeNetGazeEstimator
from gazefollower.camera import WebCamCamera
from gazefollower.misc import DefaultConfig, CalibrationMode


# ---------------------------------------------------------------------------
# OneEuroFilter – copied from game_test.py
# ---------------------------------------------------------------------------
class OneEuroFilter:
    """Attempt to smooth cursor movement without adding lag.
    Adapts smoothing based on speed: heavy when still, light when moving."""

    def __init__(self, min_cutoff=2.0, beta=0.04, d_cutoff=1.0):
        self.min_cutoff = min_cutoff
        self.beta = beta
        self.d_cutoff = d_cutoff
        self.x_prev = None
        self.dx_prev = 0.0
        self.t_prev = None

    def _smoothing_factor(self, t_e, cutoff):
        r = 2 * math.pi * cutoff * t_e
        return r / (r + 1)

    def __call__(self, x, t=None):
        if t is None:
            t = time.perf_counter()
        if self.t_prev is None:
            self.t_prev = t
            self.x_prev = x
            return x
        t_e = t - self.t_prev
        if t_e <= 0:
            return self.x_prev

        a_d = self._smoothing_factor(t_e, self.d_cutoff)
        dx = (x - self.x_prev) / t_e
        dx_hat = a_d * dx + (1 - a_d) * self.dx_prev

        cutoff = self.min_cutoff + self.beta * abs(dx_hat)
        a = self._smoothing_factor(t_e, cutoff)
        x_hat = a * x + (1 - a) * self.x_prev

        self.x_prev = x_hat
        self.dx_prev = dx_hat
        self.t_prev = t
        return x_hat


# ---------------------------------------------------------------------------
# Helper: generate N random circle positions inside the screen with margin
# ---------------------------------------------------------------------------
def generate_random_points(n, screen_w, screen_h, margin=100):
    """Return a list of (x, y) tuples avoiding the extreme edges."""
    pts = []
    for _ in range(n):
        x = random.randint(margin, screen_w - margin)
        y = random.randint(margin, screen_h - margin)
        pts.append((x, y))
    return pts


# ---------------------------------------------------------------------------
# Helper: draw a centred message and wait for SPACE
# ---------------------------------------------------------------------------
def show_message_screen(win, message, bg_color=(30, 30, 30), text_color=(255, 255, 255)):
    """Fill the screen with a message and block until SPACE is pressed."""
    font = pygame.font.SysFont(None, 64)
    sub_font = pygame.font.SysFont(None, 40)
    win.fill(bg_color)
    rendered = font.render(message, True, text_color)
    hint = sub_font.render("Press SPACE to continue", True, text_color)
    win.blit(rendered, (win.get_width() // 2 - rendered.get_width() // 2,
                        win.get_height() // 2 - rendered.get_height() // 2 - 30))
    win.blit(hint, (win.get_width() // 2 - hint.get_width() // 2,
                    win.get_height() // 2 + 40))
    pygame.display.flip()

    waiting = True
    while waiting:
        for ev in pygame.event.get():
            if ev.type == KEYDOWN:
                if ev.key == K_SPACE:
                    waiting = False
                elif ev.key == K_ESCAPE:
                    pygame.quit()
                    raise SystemExit


# ---------------------------------------------------------------------------
# Core: run one phase of the accuracy test (10 points)
# ---------------------------------------------------------------------------
CIRCLE_RADIUS = 18
COLLECT_FRAMES = 10        # number of gaze frames to average
DISPLAY_SECONDS = 2.0      # how long the target is shown before collection


def run_phase(win, gf, filter_x, filter_y, bg_color, circle_color, num_points=10):
    """
    Show *num_points* random circles.  For each point:
      1. Display the circle for DISPLAY_SECONDS so the user can fixate.
      2. Collect COLLECT_FRAMES gaze samples and average them.
      3. Return a list of dicts with target/cursor coords and distance.
    """
    screen_w, screen_h = win.get_size()
    points = generate_random_points(num_points, screen_w, screen_h)
    results = []

    for idx, (tx, ty) in enumerate(points):
        # Reset filters for each new target so old state doesn't bleed
        filter_x.__init__(filter_x.min_cutoff, filter_x.beta, filter_x.d_cutoff)
        filter_y.__init__(filter_y.min_cutoff, filter_y.beta, filter_y.d_cutoff)

        # --- Draw the target circle ---
        win.fill(bg_color)
        pygame.draw.circle(win, circle_color, (tx, ty), CIRCLE_RADIUS)
        pygame.display.flip()

        # Let the user look at it for DISPLAY_SECONDS
        start = time.time()
        while time.time() - start < DISPLAY_SECONDS:
            # Pump events so the OS doesn't think we froze
            for ev in pygame.event.get():
                if ev.type == KEYDOWN and ev.key == K_ESCAPE:
                    pygame.quit()
                    raise SystemExit
            # Keep reading gaze so filters warm up
            gaze_info = gf.get_gaze_info()
            if gaze_info and gaze_info.status:
                try:
                    filter_x(gaze_info.filtered_gaze_coordinates[0])
                    filter_y(gaze_info.filtered_gaze_coordinates[1])
                except TypeError:
                    pass
            time.sleep(0.01)

        # --- Collect COLLECT_FRAMES gaze samples ---
        gx_samples = []
        gy_samples = []
        while len(gx_samples) < COLLECT_FRAMES:
            for ev in pygame.event.get():
                if ev.type == KEYDOWN and ev.key == K_ESCAPE:
                    pygame.quit()
                    raise SystemExit

            gaze_info = gf.get_gaze_info()
            if gaze_info and gaze_info.status:
                try:
                    raw_gx = gaze_info.filtered_gaze_coordinates[0]
                    raw_gy = gaze_info.filtered_gaze_coordinates[1]
                    sx = filter_x(raw_gx)
                    sy = filter_y(raw_gy)
                    gx_samples.append(sx)
                    gy_samples.append(sy)
                except TypeError:
                    pass
            time.sleep(0.01)

        avg_gx = sum(gx_samples) / len(gx_samples)
        avg_gy = sum(gy_samples) / len(gy_samples)
        dist = math.sqrt((tx - avg_gx) ** 2 + (ty - avg_gy) ** 2)

        results.append({
            "target_x": tx,
            "target_y": ty,
            "cursor_x": round(avg_gx, 2),
            "cursor_y": round(avg_gy, 2),
            "distance": round(dist, 2),
            "bg": "white" if bg_color == (255, 255, 255) else "black",
        })

        # Brief blank between targets
        win.fill(bg_color)
        pygame.display.flip()
        pygame.time.wait(300)

    return results


# ---------------------------------------------------------------------------
# Calibration-point count extraction
# ---------------------------------------------------------------------------
_WORD_TO_NUM = {
    "FIVE": 5, "NINE": 9, "THIRTEEN": 13,
    "SEVENTEEN": 17, "TWENTY_TWO": 22, "FORTY_FIVE": 45,
}


def cali_mode_to_points(mode):
    """Extract the numeric point count from a CalibrationMode enum value.
    e.g. CalibrationMode.FIVE_POINT  ->  5
         CalibrationMode.FORTY_FIVE_POINT  ->  45
    """
    name = mode.name  # e.g. 'FIVE_POINT' or 'FORTY_FIVE_POINT'
    prefix = name.replace("_POINT", "")
    if prefix in _WORD_TO_NUM:
        return _WORD_TO_NUM[prefix]
    # Fallback: try to find a number embedded in the name
    nums = re.findall(r'\d+', name)
    return int(nums[0]) if nums else 0


# ---------------------------------------------------------------------------
# CSV helpers
# ---------------------------------------------------------------------------
CSV_HEADER = ["Title", "cali_points", "bg", "point_index", "target_x", "target_y",
              "cursor_x", "cursor_y", "distance"]


def append_results_to_csv(filepath, title, cali_points, results):
    """Append one block of results to the CSV.  Creates header if file is new."""
    file_exists = os.path.isfile(filepath) and os.path.getsize(filepath) > 0
    with open(filepath, "a", newline="") as f:
        writer = csv.writer(f)
        if not file_exists:
            writer.writerow(CSV_HEADER)
        for i, r in enumerate(results, start=1):
            writer.writerow([
                title, cali_points, r["bg"], i,
                r["target_x"], r["target_y"],
                r["cursor_x"], r["cursor_y"],
                r["distance"],
            ])


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

config = DefaultConfig()
config.cali_mode = CalibrationMode.FIVE_POINT
config.tilt_calibration = False
config.tilt_calibration_vertical = False
config.recalibration_normal = False
config.split_calibration_background = False

NUM_POINTS = 10
_SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
CSV_PATH = os.path.join(_SCRIPT_DIR, "accuracy_test_results.csv")

if __name__ == "__main__":
    # --- Get test title from the terminal ---
    title = input("Enter a title for this test run: ").strip()
    if not title:
        title = "untitled"

    # --- Init pygame ---
    pygame.init()
    win = pygame.display.set_mode((1920, 1080), pygame.FULLSCREEN)

    # --- Init GazeFollower ---
    gf = GazeFollower(camera=WebCamCamera(webcam_id=0), config=config)
    gf.preview(win=win)

    # --- Menu: calibrate or use saved calibration ---
    has_saved_cali = gf.calibration.has_calibrated
    font = pygame.font.SysFont(None, 60)
    menu_running = True
    while menu_running:
        win.fill((30, 30, 30))
        title_surf = font.render("Accuracy Test", True, (255, 255, 255))
        opt_c = font.render("[C]  Calibrate", True, (100, 255, 100))
        if has_saved_cali:
            opt_s = font.render("[S]  Start with saved calibration", True, (100, 200, 255))
        else:
            opt_s = font.render("[S]  No saved calibration found", True, (120, 120, 120))
        win.blit(title_surf, (win.get_width() // 2 - title_surf.get_width() // 2, 300))
        win.blit(opt_c, (win.get_width() // 2 - opt_c.get_width() // 2, 420))
        win.blit(opt_s, (win.get_width() // 2 - opt_s.get_width() // 2, 500))
        pygame.display.flip()

        for ev in pygame.event.get():
            if ev.type == KEYDOWN:
                if ev.key == K_c:
                    gf.calibrate(win=win)
                    gf.calibration.save_model()
                    menu_running = False
                elif ev.key == K_s and has_saved_cali:
                    menu_running = False

    # --- Start gaze sampling ---
    gf.start_sampling()
    pygame.time.wait(100)

    filter_x = OneEuroFilter(min_cutoff=1.5, beta=0.01)
    filter_y = OneEuroFilter(min_cutoff=1.5, beta=0.01)

    # =======================================================================
    #  PHASE 1 – Black circles on white background
    # =======================================================================
    show_message_screen(win,
                        "Phase 1: Black circles on white background",
                        bg_color=(255, 255, 255), text_color=(0, 0, 0))

    results_phase1 = run_phase(win, gf, filter_x, filter_y,
                               bg_color=(255, 255, 255),
                               circle_color=(0, 0, 0),
                               num_points=NUM_POINTS)

    # =======================================================================
    #  BREAK
    # =======================================================================
    show_message_screen(win,
                        "Break – take a rest!",
                        bg_color=(30, 30, 30), text_color=(255, 255, 255))

    # =======================================================================
    #  PHASE 2 – White circles on black background
    # =======================================================================
    show_message_screen(win,
                        "Phase 2: White circles on black background",
                        bg_color=(0, 0, 0), text_color=(255, 255, 255))

    results_phase2 = run_phase(win, gf, filter_x, filter_y,
                               bg_color=(0, 0, 0),
                               circle_color=(255, 255, 255),
                               num_points=NUM_POINTS)

    # =======================================================================
    #  Save results
    # =======================================================================

    all_results = results_phase1 + results_phase2
    cali_points = cali_mode_to_points(config.cali_mode)
    append_results_to_csv(CSV_PATH, title, cali_points, all_results)
    print(f"\n✓ Results appended to {CSV_PATH}  ({len(all_results)} points)")

    # =======================================================================
    #  Done
    # =======================================================================
    show_message_screen(win, "Test complete! Results saved.",
                        bg_color=(30, 30, 30), text_color=(100, 255, 100))

    # Cleanup
    pygame.time.wait(100)
    gf.stop_sampling()
    gf.release()
    pygame.quit()
