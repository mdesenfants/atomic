import * as hello from 'hellojs';
import * as React from 'react';
import './App.css';
import { AtomicCounterClient, getAuthToken } from './atomic-counter/build/dist/atomicCounter';
import GoogleLogin from './GoogleLogin';

import logo from './logo.svg';

hello.init({
    google: '1076081007580-rabmg87rit0dcdcc6m29pecc35i0lj5p.apps.googleusercontent.com'
});

interface IAppState {
    client: AtomicCounterClient | null;
}

class App extends React.Component<{}, IAppState> {
    constructor(props: {}) {
        super(props);
        this.state = { client: null };
    }

    public render() {
        const callback = (value: any) => {
            getAuthToken(value).then(t => this.setState({ client: new AtomicCounterClient(t) }));
        };

        const inc = this.increment.bind(this);
        const count = this.count.bind(this);

        return (
            <div className="App">
                <header className="App-header">
                    <img src={logo} className="App-logo" alt="logo" />
                    <h1 className="App-title">Atomic Counter</h1>
                </header>
                <p className="App-intro">
                    {this.state.client ? "logged in" : null}
                </p>
                {this.state.client ? null : <GoogleLogin tokenCallback={callback} />}
                {this.state.client ? <button onClick={inc}>Increment</button> : null}
                {this.state.client ? <button onClick={count}>Count</button> : null}
            </div>
        );
    }

    private async increment(): Promise<void> {
        if (this.state.client) {
            this.state.client.increment();
        }
    }

    private async count(): Promise<void> {
        if (this.state.client) {
            alert(await this.state.client.count());
        }
    }
}

export default App;
