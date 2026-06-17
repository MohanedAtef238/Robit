# Distance Test (Accuracy Evaluation)

This directory contains scripts and tools to test, record, and analyze the distance/accuracy of the **GazeFollower** system (an eye-tracking framework). 

It measures the Euclidean distance (in pixels) between random target points shown on the screen and the user's estimated gaze coordinates.

---

## 1. Running the Accuracy Test (`accuracy_test.py`)

The script is built using `pygame` and `gazefollower` libraries. It prompts the user for a test run title and then launches a fullscreen window to perform a two-phase test.

### Requirements & Setup
Ensure you have the required packages installed:
```bash
pip install pygame pandas matplotlib numpy
```

### Steps to Run
1. Execute the script from the terminal:
   ```bash
   python accuracy_test.py
   ```
2. **Enter Test Title**: The console will prompt:
   `Enter a title for this test run: `
   *Example:* `base`, `epsilon_0.05`, or `45_frames`.
3. **Calibrate or Resume**:
   * Press **`C`** to start a new eye-tracker calibration (uses 5-point calibration by default).
   * Press **`S`** to skip calibration and use a saved model (if available).
4. **Test Execution (20 Targets total)**:
   * **Phase 1 (10 Targets)**: Displays black circles on a white background.
   * **Rest Break**: A message screen allows the user to rest their eyes. Press **`SPACE`** to continue.
   * **Phase 2 (10 Targets)**: Displays white circles on a black background.
5. **Interactive Controls**:
   * Press **`SPACE`** to transition between informational screens.
   * Press **`ESCAPE`** at any point to safely exit the test and clean up resources.

### How Data Collection Works for Each Target
1. A circle of radius 18px is generated at a random position with a 100px margin from screen boundaries.
2. The circle is displayed for **2.0 seconds** (`DISPLAY_SECONDS`) so the user can fixate on it. Meanwhile, the eye tracker's `OneEuroFilter` warms up.
3. The script records and averages **10 consecutive gaze coordinate frames** (`COLLECT_FRAMES`) to compute the final estimated coordinate (`cursor_x`, `cursor_y`).
4. The Euclidean distance between the target center and the average gaze point is computed.
5. The results are automatically appended to `accuracy_test_results.csv`.

---

## 2. Dataset Structure (`accuracy_test_results.csv`)

The data is saved as a tabular CSV file with the following columns:

| Column Header | Description |
| :--- | :--- |
| `Title` | The custom name/tag entered for the test run (e.g., `base`). |
| `cali_points` | Number of calibration points used (e.g., `5`). |
| `bg` | Background color during that phase (`white` or `black`). |
| `point_index` | Sequential index of the target in the run (1 to 20). |
| `target_x`, `target_y` | Screen coordinates of the displayed circle (pixels). |
| `cursor_x`, `cursor_y` | Estimated coordinates of the user's gaze (pixels). |
| `distance` | Euclidean distance between target and estimated gaze (pixels). |

---

## 3. Data Analysis & Visualization (`accuracy_analysis.ipynb`)

Once you have recorded one or more runs, you can analyze the tracking precision:

1. Start Jupyter Notebook 
2. Open `accuracy_analysis.ipynb`.
3. Run the cells to:
   * Load and group the data from `accuracy_test_results.csv` by run `Title` and `cali_points`.
   * View summarized stats including **Mean Distance (px)**, **Standard Deviation (Std)**, and sample size **N**.
   * Generate a bar plot comparing the mean Euclidean error across different parameter tuning trials.
