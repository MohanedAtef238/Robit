import serial
from statistics import mode

THRESHOLD = 110

def readserial(comport, baudrate):
    """
    Reads serial data from a specified port and processes EMG signals.

    Args:
        comport (str): The COM port to connect to (e.g., 'COM5').
        baudrate (int): The baud rate for the serial connection.
    """
    ser = serial.Serial(comport, baudrate, timeout=0.1)         # 1/timeout is the frequency at which the port is read

    signal_len = []
    confirmation_list = []
    while True:
        data = ser.readline().decode().strip()
        signal_value = data.split(",")
        if data:
            try:
                # print(data)
                # print(signal_value)
                # print(signal_value[2])
                
                # Append the third value from the CSV data (the best value to use)
                signal_len.append(float(signal_value[2]))
                # print(signal_len)
                # print(len(signal_len))
                
                # Process a batch of 100 samples (a window that's almost real-time yet relatively accurate)
                if len(signal_len) >= 100:
                    # print("in list")
                    # print(get_decision(signal_len))
                    
                    # Store the decision (1 or 0) in a confirmation list
                    confirmation_list.append(get_decision(signal_len))
                    
                    # If we have 3 decisions, take the mode of them to confirm the result
                    if len(confirmation_list) >= 3:
                        print(mode(confirmation_list))
                        confirmation_list = [] # Reset confirmation list
                    signal_len = [] # Reset signal buffer
            except:
                pass

def get_decision(signal_len, threshold=THRESHOLD):
    """
    Analyzes a list of signal values to determine if a trigger (e.g., muscle contraction) occurred.

    Args:
        signal_len (list): List of float signal values.
        threshold (float): The threshold value to compare the mode against. Defaults to THRESHOLD.

    Returns:
        int: 1 if the mode of the signal is within +/- 10% of the threshold, 0 otherwise.
    """
    # print("in function")
    # Check if the mode of the signal falls within the range [threshold - 10%, threshold + 10%]
    if mode(signal_len) <= threshold+threshold*.1 and mode(signal_len) >= threshold-threshold*.1:   #using mode to avoid noise, using 10% up/down to provide a range
        return 1
    else:
        return 0

    


if __name__ == '__main__':
    readserial('COM5', 115200)