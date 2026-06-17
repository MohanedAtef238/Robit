# encoding=utf-8
# Author: GC Zhu
# Email: zhugc2016@gmail.com

import time

import numpy as np

from ..logger import Log
from ..misc import FaceInfo, GazeInfo, px2cm, generate_points, CalibrationMode, cm2px


class CalibrationController:
    def __init__(self, cali_mode, camera_pos, screen_size, physical_screen_size=None, eye_blink_threshold=10):
        self.mean_euclidean_error = None
        self.cali_available = False
        self.labels = None
        self.predictions = None
        self.normalized_point = generate_points()
        self._nine_cali_idx = [23, 1, 5, 9, 19, 27, 37, 41, 45, 23]
        self._five_cali_idx = [23, 1, 9, 37, 45, 23]
        self._thirteen_cali_idx = [23, 1, 5, 9, 12, 16, 19, 27, 30, 34, 37, 41, 45, 23]
        self._seventeen_cali_idx = [23, 1, 3, 5, 9, 12, 14, 16, 19, 25, 27, 30, 32, 34, 37, 41, 45, 23]
        self._twentytwo_cali_idx = [23, 1, 3, 5, 7, 9, 10, 12, 16, 18, 19, 21, 25, 27, 28, 30, 34, 36, 37, 39, 41, 45, 23]
        self._fortyfive_cali_idx = [23, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 23]
        self._right_tilt_cali_idx = [9, 16, 18, 27, 34, 36, 45]
        self._left_tilt_cali_idx = [1, 10, 12, 19, 28, 30, 37]
        self._up_tilt_cali_idx = [1, 5, 9, 10, 12, 16, 18]
        self._down_tilt_cali_idx = [28, 30, 34, 36, 37, 41, 45]
        self._normal_recali_cali_idx = [1, 9, 23, 37, 45]

        self._six_vali_idx = [2, 8, 22, 24, 38, 44]
        self._eight_vali_idx = [2, 8, 13, 15, 31, 33, 38, 44]
        self._seventeen_vali_idx = [2, 4, 8, 11, 13, 15, 17, 20, 24, 26, 29, 31, 33, 35, 38, 42, 44]
        self._twentytwo_vali_idx = [2, 4, 6, 8, 11, 13, 14, 15, 17, 20, 24, 26, 29, 31, 32, 33, 35, 38, 40, 42, 43, 44]

        self.cam_pos = camera_pos
        self.screen_size = screen_size
        self.eye_blink_threshold = eye_blink_threshold
        self.physical_screen_size = physical_screen_size
        self.cali_mode: CalibrationMode = cali_mode

        self.x = self.screen_size[0] // 2
        self.y = self.screen_size[1] // 2
        self.progress = 0

        self._prepare_time = 1.5  # time for waiting subject look at the dot
        self._wait_time = 0.5
        self._n_frame_need_collect = 23

        self.feature_ids = []
        self.feature_vectors = []
        self.label_vectors = []
        self._n_frame_added = 0
        self._current_index = 0
        self._feature_full_time = 0
        self._each_point_onset_time = 0
        self.cali_model_fitted = False
        self.calibrating = False
        self._tilt_phase_active = False
        self._tilt_cali_idx = []
        self._tilt_num_points = 5
        self._tilt_feature_offset = 0
        self._defer_model_fitting = False
        self._break_interval = 22
        self._on_break = False
        self._break_taken = False
        self._split_background = False

    def enable_split_background(self):
        self._split_background = True
        for attr in ['_nine_cali_idx', '_five_cali_idx', '_thirteen_cali_idx', '_seventeen_cali_idx', '_twentytwo_cali_idx', '_fortyfive_cali_idx']:
            original = list(getattr(self, attr))
            setattr(self, attr, original + original)

    @property
    def is_second_half(self):
        if self._tilt_phase_active:
             # Tilt / recalibration phases always use white background
             return False
        if self._split_background:
             return self._current_index > self.cali_mode.value
        total = self.cali_mode.value
        mid = (total + 1) // 2
        return (self._current_index - 1) >= mid

    def update_position(self):
        if self._tilt_phase_active:
            position_idx = self._tilt_cali_idx[self._current_index]
        elif self.cali_mode == CalibrationMode.NINE_POINT:
            position_idx = self._nine_cali_idx[self._current_index]
        elif self.cali_mode == CalibrationMode.FIVE_POINT:
            position_idx = self._five_cali_idx[self._current_index]
        elif self.cali_mode == CalibrationMode.THIRTEEN_POINT:
            position_idx = self._thirteen_cali_idx[self._current_index]
        elif self.cali_mode == CalibrationMode.SEVENTEEN_POINT:
            position_idx = self._seventeen_cali_idx[self._current_index]
        elif self.cali_mode == CalibrationMode.TWENTY_TWO_POINT:
            position_idx = self._twentytwo_cali_idx[self._current_index]
        else:
            position_idx = self._fortyfive_cali_idx[self._current_index]

        percent_point = self.normalized_point[position_idx - 1]
        self.x = percent_point[0]
        self.y = percent_point[1]
        self.progress = int(np.round(self._n_frame_added * 100 / self._n_frame_need_collect))

    def new_session(self):
        self.feature_ids.clear()
        self.feature_vectors.clear()
        self.label_vectors.clear()
        self._n_frame_added = 0
        self._current_index = 0
        self.cali_model_fitted = False
        self.calibrating = True
        self._tilt_phase_active = False
        self._defer_model_fitting = True
        self._on_break = False
        self._break_taken = False
        self._prepare_time = 1.5  # restore normal prepare time
        if self._split_background:
            self._break_interval = self.cali_mode.value
        self.update_position()
        self._each_point_onset_time = time.time()

        for _ in range(self.cali_mode.value):
            self.feature_ids.append([])
            self.feature_vectors.append([])
            self.label_vectors.append([])

    def new_tilt_session(self, side):
        """Start a tilt calibration phase for the given side ('right', 'left', 'up', or 'down')."""
        if side == 'right':
            self._tilt_cali_idx = self._right_tilt_cali_idx
        elif side == 'left':
            self._tilt_cali_idx = self._left_tilt_cali_idx
        elif side == 'up':
            self._tilt_cali_idx = self._up_tilt_cali_idx
        elif side == 'down':
            self._tilt_cali_idx = self._down_tilt_cali_idx
        else:
            self._tilt_cali_idx = self._normal_recali_cali_idx

        self._tilt_phase_active = True
        self._tilt_num_points = len(self._tilt_cali_idx)
        self._tilt_feature_offset = len(self.feature_vectors)
        self._n_frame_added = 0
        self._current_index = 0
        self.calibrating = True
        self.cali_model_fitted = False
        self._prepare_time = 1.0  # shorter prepare time for tilt phases

        for _ in range(self._tilt_num_points):
            self.feature_ids.append([])
            self.feature_vectors.append([])
            self.label_vectors.append([])

        self.update_position()
        self._each_point_onset_time = time.time()
        self._on_break = False

    def resume_from_break(self):
        """Resume calibration after a break point."""
        self._on_break = False
        self._each_point_onset_time = time.time()

    def add_cali_feature(self, gaze_info: GazeInfo, face_info: FaceInfo):
        if self._on_break:
            return
        # Determine stop condition based on phase
        if self._tilt_phase_active:
            stop_index = self._tilt_num_points
        else:
            stop_index = self.cali_mode.value + 1
            if self._split_background:
                stop_index = 2 * (self.cali_mode.value + 1)

        if self._current_index == stop_index:
            Log.i("calibrating shutdowns")
            self.calibrating = False
            return
        self.update_position()
        if (time.time() - self._each_point_onset_time) >= self._prepare_time:
            if gaze_info.status and (self._n_frame_added < self._n_frame_need_collect) and (
                    face_info.left_eye_openness > self.eye_blink_threshold) and (
                    face_info.right_eye_openness > self.eye_blink_threshold):

                # Determine storage index
                if self._tilt_phase_active:
                    # Tilt mode: collect from index 0 (no warm-up skip)
                    should_collect = self._n_frame_added < self._n_frame_need_collect
                    store_idx = self._tilt_feature_offset + self._current_index
                else:
                    # Normal mode: skip warm-up points (index 0, and second warm-up in split mode)
                    is_warmup = self._current_index == 0
                    if self._split_background and self._current_index == self.cali_mode.value + 1:
                        is_warmup = True
                    should_collect = not is_warmup and self._n_frame_added < self._n_frame_need_collect
                    if self._split_background and self._current_index > self.cali_mode.value + 1:
                        store_idx = self._current_index - self.cali_mode.value - 2
                    else:
                        store_idx = self._current_index - 1

                if should_collect:
                    self.feature_vectors[store_idx].append(gaze_info.features)
                    self.feature_ids[store_idx].append([store_idx])

                    if self.physical_screen_size:
                        # has physical_screen_size
                        added_pos = px2cm((self.x * self.screen_size[0], self.y * self.screen_size[1]),
                                          self.cam_pos, self.physical_screen_size, self.screen_size)
                    else:
                        added_pos = [self.x, self.y]
                    self.label_vectors[store_idx].append(added_pos)

                self._n_frame_added += 1
                if self._n_frame_added == self._n_frame_need_collect:
                    self._feature_full_time = time.time()

            if self._n_frame_added == self._n_frame_need_collect:
                if time.time() - self._feature_full_time >= self._wait_time:
                    self._current_index += 1
                    self._n_frame_added = 0
                    self._each_point_onset_time = time.time()

                    # Check for break point (every _break_interval data points)
                    if not self._tilt_phase_active and self._break_interval > 0 and self._current_index > 1 and not self._break_taken:
                        data_points_collected = self._current_index - 1
                        if data_points_collected % self._break_interval == 0 and self._current_index < stop_index:
                            self._on_break = True
                            self._break_taken = True

    def set_calibration_results(self, has_calibrated, mean_euclidean_error, labels, predictions):
        self.cali_available = has_calibrated
        self.mean_euclidean_error = mean_euclidean_error
        self.labels = labels
        self.predictions = predictions

    def convert_to_pixel(self, raw_pos):
        """
        Convert raw position values to pixel coordinates.

        The raw position could be either percentage-based (relative to screen dimensions)
        or in physical centimeters, depending on whether physical screen size is specified.

        Args:
            raw_pos (tuple): Input position coordinates, could be either
                (percentage_x, percentage_y) if using relative units, or
                (cm_x, cm_y) if physical screen size is available.

        Returns:
            tuple: Pixel coordinates (x, y) converted based on input format.
                Uses cm2px conversion if physical screen size exists, otherwise
                interprets input as screen percentages.

        Note:
            Requires either physical_screen_size for centimeter conversion or
            screen_size for percentage conversion to be properly configured.
        """
        if self.physical_screen_size:
            return cm2px(raw_pos, self.cam_pos, self.physical_screen_size, self.screen_size)
        else:
            return raw_pos[0] * self.screen_size[0], raw_pos[1] * self.screen_size[1]
