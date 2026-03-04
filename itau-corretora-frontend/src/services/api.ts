
import axios from 'axios';

const api = axios.create({
  baseURL: 'http://localhost:5254/api', 
  headers: {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
  },
  timeout: 10000, 
});

api.interceptors.response.use(
  (response) => {
    return response;
  },
  (error) => {
    console.error('Erro na requisição da API:', error?.response?.data || error.message);
    return Promise.reject(error);
  }
);

export default api;