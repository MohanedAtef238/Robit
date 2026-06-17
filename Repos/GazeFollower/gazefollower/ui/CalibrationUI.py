# encoding=utf-8
# Author: GC Zhu
# Email: zhugc2016@gmail.com

import cv2
import numpy as np

from gazefollower.calibration import CalibrationController
from .BaseUI import BaseUI
from ..misc import DefaultConfig


class CalibrationUI(BaseUI):
    def __init__(self, win, backend_name: str = "PyGame", bg_color=(255, 255, 255),
                 config: DefaultConfig = DefaultConfig()):
        """
        Initializes the Calibration UI.
        """
        super().__init__(win, backend_name, bg_color)

        self.config = config
        self.error_bar_color = (0, 255, 0)  # Green color for the error bar
        self.error_bar_thickness = 2  # Thickness of the error bar lin

        self._sound_id = "beep"
        self.backend.load_sound(self.config.cali_target_sound, self._sound_id)

        # Create colour-inverted calibration target for black background
        _img = cv2.imread(self.config.cali_target_img)
        _img_rgb = cv2.cvtColor(_img, cv2.COLOR_BGR2RGB)
        self._inverted_cali_img = (255 - _img_rgb).astype(np.uint8)

        self.target_position: tuple = (960, 540)
        self.target_progress: int = 0

        self.point_showing = False
        self.model_fitting_showing = False
        self.running = False

    def draw_guidance(self, instruction_text):
        """Draws the guidance text for the user."""
        self.running = True
        # texts = instruction_text.split("\n")
        while self.running:
            # listen event
            self.backend.listen_event(self)
            # for pygame
            self.backend.before_draw()
            # draw texts
            self.backend.draw_text_on_screen_center(instruction_text, self.font_name, self.font_size)
            # flip the screen
            self.backend.after_draw()

    def draw_cali_result(self, cali_controller: CalibrationController, model_fit_instruction: str) -> bool:
        """
        Return False to continue the calibration progress and Return True to stop the calibration.
        """
        while not cali_controller.cali_model_fitted:
            self.backend.listen_event(self, skip_event=True)
            # for pygame
            self.backend.before_draw()
            # draw texts
            self.backend.draw_text_on_screen_center(model_fit_instruction, self.font_name, self.font_size)
            # flip the screen
            self.backend.after_draw()

        self.running = True
        if cali_controller.cali_available:
            text = "Calibration succeed."
        else:
            text = "Calibration failed."

        uni_p, avg_labels, avg_predictions = [], [], []
        if cali_controller.predictions is not None:
            text += "\nRed dot: ground truth point, Green dot: predicted point"
            # Filter out empty buckets and concatenate (sub-lists may
            # have different frame counts after tilt calibration).
            id_arrays = [np.array(i) for i in cali_controller.feature_ids if len(i) > 0]
            point_ids = np.concatenate(id_arrays, axis=0).reshape(-1)

            label_arrays = [np.array(l) for l in cali_controller.label_vectors if len(l) > 0]
            labels_flat = np.concatenate(label_arrays, axis=0)
            label_dim = labels_flat.shape[1]

            predictions_flat = np.array(cali_controller.predictions)

            uni_p = np.unique(point_ids)
            avg_labels = np.zeros((len(uni_p), label_dim))
            avg_predictions = np.zeros((len(uni_p), predictions_flat.shape[1]))

            for idx, point_id in enumerate(uni_p):
                mask = (point_ids == point_id)

                avg_label = np.mean(labels_flat[mask], axis=0)
                avg_pred = np.mean(predictions_flat[mask], axis=0)

                avg_labels[idx] = cali_controller.convert_to_pixel(avg_label)
                avg_predictions[idx] = cali_controller.convert_to_pixel(avg_pred)

        text += "\nPress `Space` to continue OR `R` to recalibration"
        while self.running:
            key = self.backend.listen_keys(key=('space', 'r'))
            if key == 'space':
                return True
            elif key == 'r':
                return False
            self.backend.before_draw()
            self.backend.draw_text_in_bottom_right_corner(
                text, self.font_name, self.row_font_size,
                text_color=self._color_black)

            if cali_controller.predictions is not None:
                for n, _ in enumerate(uni_p):
                    avg_label = avg_labels[n]
                    avg_prediction = avg_predictions[n]
                    self.backend.draw_circle(avg_label[0], avg_label[1], 4, self._color_red)
                    self.backend.draw_circle(avg_prediction[0], avg_prediction[1], 4, self._color_green)
                    self.backend.draw_line(avg_label[0], avg_label[1], avg_prediction[0], avg_prediction[1],
                                           self._color_gray, line_width=2)
            self.backend.after_draw()

    def new_session(self):
        self.running = True

    def draw_tilt_instruction(self, direction):
        """Draw instruction to tilt head in the given direction with an arrow."""
        self.running = True
        instruction = f"Please tilt your head slightly to the {direction}"
        sub_text = "Press SPACE to continue"

        while self.running:
            self.backend.listen_event(self)
            self.backend.before_draw()

            sw, sh = self.backend.get_screen_size()

            # Draw main instruction text
            self.backend.draw_text(instruction, self.font_name, self.font_size, self._color_black,
                                   (0, sh // 2 - 80, sw, 40), align='center')

            # Draw arrow
            arrow_length = 200
            arrow_head_size = 30
            cx = sw // 2
            cy = sh // 2

            if direction == 'right':
                x1 = cx - arrow_length // 2
                x2 = cx + arrow_length // 2
                # Shaft
                self.backend.draw_line(x1, cy, x2, cy, self._color_black, 4)
                # Arrowhead
                self.backend.draw_line(x2, cy, x2 - arrow_head_size, cy - arrow_head_size,
                                       self._color_black, 4)
                self.backend.draw_line(x2, cy, x2 - arrow_head_size, cy + arrow_head_size,
                                       self._color_black, 4)
            elif direction == 'left':
                x1 = cx + arrow_length // 2
                x2 = cx - arrow_length // 2
                # Shaft
                self.backend.draw_line(x1, cy, x2, cy, self._color_black, 4)
                # Arrowhead
                self.backend.draw_line(x2, cy, x2 + arrow_head_size, cy - arrow_head_size,
                                       self._color_black, 4)
                self.backend.draw_line(x2, cy, x2 + arrow_head_size, cy + arrow_head_size,
                                       self._color_black, 4)
            elif direction == 'up':
                y1 = cy + arrow_length // 2
                y2 = cy - arrow_length // 2
                # Shaft
                self.backend.draw_line(cx, y1, cx, y2, self._color_black, 4)
                # Arrowhead
                self.backend.draw_line(cx, y2, cx - arrow_head_size, y2 + arrow_head_size,
                                       self._color_black, 4)
                self.backend.draw_line(cx, y2, cx + arrow_head_size, y2 + arrow_head_size,
                                       self._color_black, 4)
            else:  # down
                y1 = cy - arrow_length // 2
                y2 = cy + arrow_length // 2
                # Shaft
                self.backend.draw_line(cx, y1, cx, y2, self._color_black, 4)
                # Arrowhead
                self.backend.draw_line(cx, y2, cx - arrow_head_size, y2 - arrow_head_size,
                                       self._color_black, 4)
                self.backend.draw_line(cx, y2, cx + arrow_head_size, y2 - arrow_head_size,
                                       self._color_black, 4)

            # Draw sub text
            self.backend.draw_text(sub_text, self.font_name, self.row_font_size, self._color_gray,
                                   (0, sh // 2 + 60, sw, 30), align='center')

            self.backend.after_draw()

    def draw(self, cali_controller: CalibrationController):
        last_x, last_y = -1, -1
        while cali_controller.calibrating:
            # Check for break point
            if cali_controller._on_break:
                self._draw_break_screen(cali_controller)
                last_x, last_y = -1, -1
                continue

            # listen event
            self.backend.listen_event(self, skip_event=True)
            # for pygame
            is_black_bg = False
            if getattr(self.config, 'split_calibration_background', False):
                if cali_controller.is_second_half:
                    if hasattr(self.backend, 'bg_color'):
                        self.backend.bg_color = (0, 0, 0)
                        is_black_bg = True
                else:
                    if hasattr(self.backend, 'bg_color'):
                        self.backend.bg_color = (255, 255, 255)

            self.backend.before_draw()
            # draw dot
            cali_img_size = self.config.cali_target_size
            target_x = int(np.round(cali_controller.x * self.backend.get_screen_size()[0]))
            target_y = int(np.round(cali_controller.y * self.backend.get_screen_size()[1]))
            draw_rect = (target_x - cali_img_size[0] // 2, target_y - cali_img_size[1] // 2,
                         cali_img_size[0], cali_img_size[1])
            if target_x != last_x or target_y != last_y:
                self.backend.play_sound(self._sound_id)
                last_x, last_y = target_x, target_y

            if is_black_bg:
                self.backend.draw_image(self._inverted_cali_img, draw_rect)
            else:
                self.backend.draw_image(self.config.cali_target_img, draw_rect)

            # On white bg: white text visible on black center of dot
            # On black bg: black text visible on white center of inverted dot
            text_color = self._color_black if is_black_bg else self._color_white
            self.backend.draw_text(str(cali_controller.progress), self.font_name, self.row_font_size, text_color,
                                   draw_rect)
            # flip the screen
            self.backend.after_draw()

        # Reset the background color to white after calibration is finished
        if hasattr(self.backend, 'bg_color'):
            self.backend.bg_color = (255, 255, 255)

    def _draw_break_screen(self, cali_controller: CalibrationController):
        """Show break message and wait for SPACE before resuming."""
        self.running = True
        break_text = ("Break point, please return to the same position as much as possible\n"
                      "Press SPACE to continue")
        while self.running:
            self.backend.listen_event(self)
            self.backend.before_draw()
            self.backend.draw_text_on_screen_center(break_text, self.font_name, self.font_size)
            self.backend.after_draw()
        cali_controller.resume_from_break()
