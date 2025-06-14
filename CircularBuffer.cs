using System;
using UnityEngine;

namespace LethalMic
{
    /// <summary>
    /// A simple circular buffer implementation for audio data
    /// </summary>
    public class CircularBuffer
    {
        private readonly float[] _buffer;
        private int _writeIndex;
        private int _readIndex;
        private int _count;
        private readonly object _lock = new object();
        
        public int Capacity { get; }
        public int Count 
        { 
            get 
            { 
                lock (_lock) 
                { 
                    return _count; 
                } 
            } 
        }
        
        public CircularBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentException("Capacity must be positive", nameof(capacity));
                
            Capacity = capacity;
            _buffer = new float[capacity];
            _writeIndex = 0;
            _readIndex = 0;
            _count = 0;
        }
        
        public void Write(float[] data)
        {
            if (data == null || data.Length == 0)
                return;
                
            lock (_lock)
            {
                foreach (float sample in data)
                {
                    _buffer[_writeIndex] = sample;
                    _writeIndex = (_writeIndex + 1) % Capacity;
                    
                    if (_count < Capacity)
                    {
                        _count++;
                    }
                    else
                    {
                        // Buffer is full, advance read index
                        _readIndex = (_readIndex + 1) % Capacity;
                    }
                }
            }
        }
        
        public int Read(float[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset >= buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));
                
            lock (_lock)
            {
                int samplesRead = System.Math.Min(count, _count);
                
                for (int i = 0; i < samplesRead; i++)
                {
                    buffer[offset + i] = _buffer[_readIndex];
                    _readIndex = (_readIndex + 1) % Capacity;
                    _count--;
                }
                
                return samplesRead;
            }
        }
        
        public void Clear()
        {
            lock (_lock)
            {
                _writeIndex = 0;
                _readIndex = 0;
                _count = 0;
                Array.Clear(_buffer, 0, _buffer.Length);
            }
        }
        
        public float[] ToArray()
        {
            lock (_lock)
            {
                float[] result = new float[_count];
                int tempReadIndex = _readIndex;
                
                for (int i = 0; i < _count; i++)
                {
                    result[i] = _buffer[tempReadIndex];
                    tempReadIndex = (tempReadIndex + 1) % Capacity;
                }
                
                return result;
            }
        }
    }
}