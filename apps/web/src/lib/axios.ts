import axios from 'axios'

export const API_REQUEST_TIMEOUT_MS = 20_000

export const apiClient = axios.create({
  baseURL: '/api',
  timeout: API_REQUEST_TIMEOUT_MS,
  headers: {
    Accept: 'application/json',
  },
})
