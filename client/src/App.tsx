import * as hello from 'hellojs';
import * as React from 'react';
import './App.css';

import { AtomicCounterClient, getAuthToken } from './atomic-counter/build/dist/atomicCounter';
import { Counter } from './Counter';

import GoogleLogin from './GoogleLogin';

hello.init({
    google: '1076081007580-rabmg87rit0dcdcc6m29pecc35i0lj5p.apps.googleusercontent.com'
});

interface IAppState {
    client: AtomicCounterClient | null;
    count: number;
    timeoutHandle: NodeJS.Timeout | null;
}

class App extends React.Component<{}, IAppState> {
    constructor(props: {}) {
        super(props);
        this.state = { client: null, count: 0, timeoutHandle: null };
    }

    public render() {
        const callback = (value: any) => {
            this.setState({ client: new AtomicCounterClient(() => getAuthToken(value, 'google'))});
        };

        return (
            <div className="App">
                <h1>Atomic Counter</h1>
                {this.state.client ? null : <GoogleLogin tokenCallback={callback} />}
                {this.state.client ? <Counter client={this.state.client} /> : null}
            </div>
        );
    }
}

export default App;
