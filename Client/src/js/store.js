/* global process, require */

import { createStore, applyMiddleware, compose } from 'redux';
import thunk from 'redux-thunk';

import allReducers from './reducers/all';


const composeEnhancers =
  typeof window === 'object' &&
  window.__REDUX_DEVTOOLS_EXTENSION_COMPOSE__ ?
    window.__REDUX_DEVTOOLS_EXTENSION_COMPOSE__({
      // Specify extension’s options like name, actionsBlacklist, actionsCreators, serialize...
    }) : compose;

const middleware = [
  thunk,
];

if (process.env.NODE_ENV !== 'production') {
  // Only add this redux store mutation detection middleware in dev
  const freeze = require('redux-freeze');
  middleware.push(freeze);
}

// Note passing middleware as the last argument to createStore requires redux@>=3.1.0
const store = createStore(
  allReducers,
  composeEnhancers(applyMiddleware(...middleware))
);

export default store;
