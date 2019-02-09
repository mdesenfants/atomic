import * as React from 'react';
import * as ReactDOM from 'react-dom';
import GoogleLogin from './GoogleLogin';

it('renders without crashing', () => {
  const div = document.createElement('div');
  // tslint:disable-next-line:jsx-no-lambda
  ReactDOM.render(<GoogleLogin hidden={false} tokenCallback={tok => tok} />, div);
  ReactDOM.unmountComponentAtNode(div);
});
