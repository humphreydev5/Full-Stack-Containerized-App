import { useEffect, useState } from 'react';

function App() {
  const [data, setData] = useState({ message: 'Loading...', cached: false });

  useEffect(() => {
    // 1. Correct the Vite environment accessor syntax
    // 2. Fallback to localhost:5000 automatically if the container variable is missing
    const apiBase = import.meta.env.VITE_API_URL || 'http://localhost:5000';

    fetch(`${apiBase}/api/data`)
      .then(res => {
        if (!res.ok) throw new Error('Network response failure');
        return res.json();
      })
      .then(data => setData(data))
      .catch(() => setData({ message: 'Error connecting to backend API', cached: false }));
  }, []);

  return (
    <div style={{ padding: '40px', fontFamily: 'sans-serif', maxWidth: '600px', margin: '0 auto' }}>
      <div style={{ border: '1px solid #ddd', padding: '20px', borderRadius: '8px', boxShadow: '0 4px 6px rgba(0,0,0,0.05)' }}>
        <h1 style={{ color: '#333', fontSize: '24px' }}>Full-Stack Containerized App</h1>
        <hr style={{ border: '0', borderTop: '1px solid #eee', margin: '20px 0' }} />
        <p><strong>Backend Status Message:</strong> {data.message}</p>
        <p><strong>Served from Redis Cache?</strong> {data.cached ? '✅ Yes' : '❌ No'}</p>
      </div>
    </div>
  );
}

export default App;