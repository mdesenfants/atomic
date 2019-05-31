import * as React from 'react';
import * as ReactDOM from 'react-dom';
import StripeLogin from './StripeLogin';

it('renders without crashing', () => {
  const div = document.createElement('div');
  // tslint:disable-next-line:jsx-no-lambda
  ReactDOM.render(<StripeLogin tokenCallback={tok => tok} />, div);
  ReactDOM.unmountComponentAtNode(div);
});
