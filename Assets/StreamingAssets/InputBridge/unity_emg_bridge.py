import asyncio
import collections
import os
import sys
import time

import numpy as np


def main():
    sys.path.insert(0, os.getcwd())

    from config import BUFFER_SIZE, EMG_BAUD, EMG_INPUT_MODE, EMG_PORT, SIMULATION_DURATION
    from emg import EMGReader
    from feature_engineering import FeatureEngineer
    from logger import Logger
    from model import EMGModel
    from constants import LOGGING_INTERVAL

    use_keyboard_simulation = EMG_INPUT_MODE == "KEYBOARD"
    reader = EMGReader(
        port=EMG_PORT,
        baud=EMG_BAUD,
        simulate_with_space=use_keyboard_simulation,
        simulation_duration=SIMULATION_DURATION,
    )
    time.sleep(2)

    logger = Logger("unity_livestream")
    stream = logger.live_stream_generator(
        session_id="unity_live_session",
        level_number=1,
        get_value_callable=lambda: reader.envelope,
        interval=LOGGING_INTERVAL,
    )

    model = EMGModel.load(os.path.join("Models", "best_model.h5"))
    if model is None:
        print("EMG bridge could not load the trained model.", flush=True)
        reader.stop()
        return

    async def run():
        buffer = collections.deque(maxlen=BUFFER_SIZE)
        last_state = None
        stream_iter = iter(stream)

        try:
            while True:
                value = await asyncio.to_thread(next, stream_iter, None)
                if value is None:
                    break

                buffer.append(value)
                prediction = False

                if len(buffer) == BUFFER_SIZE:
                    features = FeatureEngineer.calculate_emg_features(list(buffer))
                    feature_vector = np.array([[features["DASDV"], features["MYOP"]]])
                    prediction = bool(model.predict(feature_vector))

                if prediction != last_state:
                    print(f"EMG:{1 if prediction else 0}", flush=True)
                    last_state = prediction

                await asyncio.sleep(0)
        finally:
            reader.stop()

    asyncio.run(run())


if __name__ == "__main__":
    main()
